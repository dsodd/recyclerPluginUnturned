## Example config
```
<?xml version="1.0" encoding="utf-8"?>
<ItemRecyclerConfiguration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Recyclers>
    <RecyclerStorage>368</RecyclerStorage>
    <RecycledStorage>366</RecycledStorage>
  </Recyclers>
  <Items>
    <item>
      <Id>363</Id>
      <RecycleTime>5000</RecycleTime>
      <recycledIds>
        <unSignedByte>67</unSignedByte>
        <unSignedByte>67</unSignedByte>
      </recycledIds>
    </item>
    <item>
      <Id>363</Id>
      <RecycleTime>5000</RecycleTime>
      <recycledIds>
        <unSignedByte>67</unSignedByte>
        <unSignedByte>67</unSignedByte>
      </recycledIds>
    </item>
  </Items>
</ItemRecyclerConfiguration>
```
## Each item is seperated like this
```
    <item>
      <Id>363</Id>
      <RecycleTime>5000</RecycleTime>
      <recycledIds>
        <unSignedByte>67</unSignedByte>
        <unSignedByte>67</unSignedByte>
      </recycledIds>
    </item>
```
## Doccumentation for the config
> `<Id>` - the item ur recycling
> `<RecycleTime>` - how long said item will be recycling for (counted in ms(1000ms = 1 second))
> `<recycledIds>` - is the items you will receive from recycling the item, each item you need to get should be in a separate line with `<unSignedByte>itemId</unSignedByte>`
> `<RecyclerStorage>` - the storage container in which you should put items in
> `<RecycledStorage>` - where said items will end up in