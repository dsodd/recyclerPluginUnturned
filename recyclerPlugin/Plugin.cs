using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Events;
using UnityEngine;
using SDG.Unturned;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Logger = Rocket.Core.Logging.Logger;

public class Plugin : RocketPlugin<Configuration>
{
    private Dictionary<ushort, RecyclableItem> recyclableItems;

    // queue per storage instance to handle recycling process
    private readonly Dictionary<Items, Queue<(ItemJar jar, RecyclableItem recItem)>> recyclingQueues = new Dictionary<Items, Queue<(ItemJar jar, RecyclableItem recItem)>>();
    private readonly HashSet<Items> activeRecyclers = new HashSet<Items>();
    private readonly Dictionary<Items, Coroutine> timeoutCoroutines = new Dictionary<Items, Coroutine>();

    protected override void Load()
    {
        recyclableItems = Configuration.Instance.Items.ToDictionary(item => item.Id, item => item);

        BarricadeManager.onBarricadeSpawned += OnBarricadeSpawned;

        Level.onLevelLoaded += OnLevelLoaded;

        Logger.Log("Item Recycler Plugin loaded!");
    }

    protected override void Unload()
    {
        BarricadeManager.onBarricadeSpawned -= OnBarricadeSpawned;
        Level.onLevelLoaded -= OnLevelLoaded;
        Logger.Log("Item Recycler Plugin unloaded."); 
    }

    private void OnLevelLoaded(int i)
    {
        StartCoroutine(DelayedRegisterExistingRecyclers());  // register existing recyclers after a short delay
    }

    private IEnumerator DelayedRegisterExistingRecyclers()
    {
        yield return new WaitForEndOfFrame();  // wait until the next frame for proper initialization

        // go through all barricades to find and recyclers
        foreach (var region in BarricadeManager.BarricadeRegions)
        {
            foreach (var drop in region.drops)
            {
                if (drop.asset.id == Configuration.Instance.Recyclers.RecyclerInput && drop.interactable != null)
                {
                    OnBarricadeSpawned(region, drop);
                }
            }
        }

        Logger.Log("Finished registering existing recyclers.");
    }

    // handles the logic for when a barricade (recycler) spawns in the game
    private void OnBarricadeSpawned(BarricadeRegion region, BarricadeDrop drop)
    {
        if (drop.asset.id != Configuration.Instance.Recyclers.RecyclerInput) return;  // ensure it's a recycler

        InteractableStorage storage = drop.interactable as InteractableStorage;
        if (storage == null || storage.items == null) return;  // ensure the storage is valid

        HashSet<ItemJar> lastKnownJars = new HashSet<ItemJar>(storage.items.items);

        // monitor item state changes in the storage
        storage.items.onStateUpdated += () =>
        {
            foreach (ItemJar jar in storage.items.items)
                {
                // check if new items have been added and need recycling
                if (!lastKnownJars.Contains(jar) &&
                    recyclableItems.TryGetValue(jar.item.id, out RecyclableItem recItem))
                {
                    if (!recyclingQueues.ContainsKey(storage.items))
                        recyclingQueues[storage.items] = new Queue<(ItemJar, RecyclableItem)>();

                    recyclingQueues[storage.items].Enqueue((jar, recItem));

                    // start processing the recycling queue if not already active
                    if (!activeRecyclers.Contains(storage.items))
                    {
                        activeRecyclers.Add(storage.items);
                        StartCoroutine(ProcessQueue(drop.model.transform.position, storage.items));
                    }
                }
            }

            lastKnownJars = new HashSet<ItemJar>(storage.items.items);
        };
    }

    // processes the recycling queue for a specific storage
    private IEnumerator ProcessQueue(Vector3 origin, Items items)
    {
        // time to wait before stopping the coroutine if queue is empty
        const float idleTimeBeforeStop = 2f;  

        while (true)
        {
            // if theres nothing to recycle, wait a bit and check again
            while (!recyclingQueues.ContainsKey(items) || recyclingQueues[items].Count == 0)
            {
                yield return new WaitForSeconds(idleTimeBeforeStop);

                if (!recyclingQueues.ContainsKey(items) || recyclingQueues[items].Count == 0)
                {
                    activeRecyclers.Remove(items);
                    yield break;
                }
            }

            // get the next recyclable item
            var (jar, recItem) = recyclingQueues[items].Dequeue();  

            // wait for the recycle time
            yield return new WaitForSeconds(recItem.RecycleTime / 1000f);  

            if (!items.items.Contains(jar))
            {
                Logger.Log("Item removed before recycling could occur, skipping.");
                continue;
            }

            int index = items.items.IndexOf(jar);
            if (index != -1)
            {
                // remove the item from storage
                items.removeItem((byte)index);  
            }
            else
            {
                Logger.LogWarning("Could not find item to remove during recycling.");
                continue;
            }

            // find the closest storage for recycled items
            InteractableStorage targetStorage = FindClosestStorage(origin, Configuration.Instance.Recyclers.RecyclerOutput);
            if (targetStorage == null)
            {
                Logger.LogWarning("No recycledStorage found nearby.");
                continue;
            }

            // add the recycled items to the target storage
            foreach (var output in recItem.OutputItemIDs)
            {
                for (int i = 0; i < output.Amount; i++)
                {
                    Item newItem = new Item(output.Id, true);
                    bool placed = false;

                    while (!placed)
                    {
                        placed = targetStorage.items.tryAddItem(newItem);

                        if (!placed)
                        {
                            Logger.Log($"Storage full. Dropping item {output.Id} in the world...");

                            Vector3 dropPosition = targetStorage.transform.position + Vector3.up * 2f;

                            // drop on the floor
                            ItemManager.dropItem(newItem, dropPosition, true, false, false);

                            break; // item has been dropped
                        }
                    }
                }
            }
        }
    }

    // finds the closest instance of the storage specified in configuration.xml (RecycledStorage)
    private InteractableStorage FindClosestStorage(Vector3 origin, ushort targetId)
    {
        InteractableStorage closest = null;
        float closestDist = float.MaxValue;

        foreach (var region in BarricadeManager.BarricadeRegions)
        {
            foreach (var drop in region.drops)
            {
                if (drop.asset.id != targetId) continue;

                InteractableStorage storage = drop.interactable as InteractableStorage;
                if (storage == null) continue;

                float dist = Vector3.Distance(origin, drop.model.transform.position);
                if (dist < closestDist)
                {
                    closest = storage;
                    closestDist = dist;
                }
            }
        }

        return closest;
    }
    private IEnumerator ResumeAfterTimeout(Items items, Vector3 origin, float timeout)
    {
        yield return new WaitForSeconds(timeout);

        if (!activeRecyclers.Contains(items))
        {
            activeRecyclers.Add(items);
            StartCoroutine(ProcessQueue(origin, items));
        }
    }
}