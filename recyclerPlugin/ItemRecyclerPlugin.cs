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

public class ItemRecyclerPlugin : RocketPlugin<ItemRecyclerConfiguration>
{
    public static ItemRecyclerPlugin Instance;

    private Dictionary<ushort, RecyclableItem> recyclableItems;
    private ushort recyclerStorageId;
    private ushort recycledStorageId;

    // queue per storage instance to handle recycling process
    private readonly Dictionary<Items, Queue<(ItemJar jar, RecyclableItem recItem)>> recyclingQueues = new Dictionary<Items, Queue<(ItemJar jar, RecyclableItem recItem)>>();
    private readonly HashSet<Items> activeRecyclers = new HashSet<Items>();

    private bool hasInitialized = false;

    protected override void Load()
    {
        Instance = this;

        recyclerStorageId = Configuration.Instance.Recyclers.RecyclerStorage;
        recycledStorageId = Configuration.Instance.Recyclers.RecycledStorage;

        recyclableItems = Configuration.Instance.Items.ToDictionary(item => item.Id, item => item);

        // listen for barricade spawns
        BarricadeManager.onBarricadeSpawned += OnBarricadeSpawned;  

        // subscribe to player connection event to initialize recyclers after the first player joins
        U.Events.OnPlayerConnected += OnFirstPlayerConnected;

        Logger.Log("Item Recycler Plugin loaded!");
    }

    // unloads the plugin and cleans up events
    protected override void Unload()
    {
        BarricadeManager.onBarricadeSpawned -= OnBarricadeSpawned;
        U.Events.OnPlayerConnected -= OnFirstPlayerConnected; // unsubscribe from player connection event
        Logger.Log("Item Recycler Plugin unloaded.");
    }

    // run when the first player connects to register all recyclers
    private void OnFirstPlayerConnected(Rocket.Unturned.Player.UnturnedPlayer player)
    {
        if (hasInitialized) return;

        hasInitialized = true;
        StartCoroutine(DelayedRegisterExistingRecyclers());  // register existing recyclers after a short delay
    }

    // registers existing recyclers after the first player connects
    private IEnumerator DelayedRegisterExistingRecyclers()
    {
        yield return new WaitForEndOfFrame();  // wait until the next frame for proper initialization

        // go through all barricades to find and recyclers
        foreach (var region in BarricadeManager.BarricadeRegions)
        {
            foreach (var drop in region.drops)
            {
                if (drop.asset.id == recyclerStorageId && drop.interactable != null)
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
        if (drop.asset.id != recyclerStorageId) return;  // ensure it's a recycler

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
            InteractableStorage targetStorage = FindClosestStorage(origin, recycledStorageId);
            if (targetStorage == null)
            {
                Logger.LogWarning("No recycledStorage found nearby.");
                continue;
            }

            // add the recycled items to the target storage
            foreach (ushort id in recItem.RecycledIds)
            {
                Item newItem = new Item(id, true);
                targetStorage.items.addItem(0, 0, 0, newItem);
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

        return closest;  // return the closest storage found
    }
}