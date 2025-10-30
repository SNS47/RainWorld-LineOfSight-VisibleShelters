# Bug Notes

## 

1. It seems that there are more than 1 duplicates for each shelters.
One is staying in the original shelter sprite location, and the other is following the screen.
The one that follows the screen is not visible outside FoV. The one that stays is visible outside FoV.
2. To prove it, go to schorched district, room H01SKY, climb the horizontal beam at the center right of room. Then
Make the shelter sprite visible. Slowly move to the right beam, towards the edge of the right screen to nudge the camera to the right slightly.
The two sprites on top of one shelter will be visible individually.
3. It seems that the ones inside `duplicateShelterSprites` is the ones that are visible outside FoV.

##

It seems that a room with multiple screens size will not initiate the shadow.

If I were to wake up in a shelter, go outside into the multiple screens room outside shelter, the shadow will not present. Let's say it's room A.
When I visit another room with a shelter sprite, say room B, the shadow sprite from room A will appear. No matter how many screens room B has.
If room B has many screens, the shadow of room B will not appear immediately, rather it will only show up after I discover other room with shelter sprite.
Back and forth between the two rooms (TI03 & H01SKY) will result in: A (A not), B (A yes, B not), A (A yes, B yes)

Scratch the last part. This will also trigger the shadow: Shelter A -> Room A -> Shelter B
Room wrapping between shelters S02 & S03 will ...

## Things to try

- Move the sprite creation from 'every frame' to 'every room loaded'
- Find out which one is following the screen, and then hide it
- Clear the `duplicateShelterSprites` after every room loaded
- Find out which function fires when I enters a new room. Not only new ones, but also previous rooms.
    - FOUND: Looks like `InitiateSprites` fires every room changes. `LateUpdate()`, `DrawSprites()`, and `Update()` fires constantly without changing rooms.
- Initate couple of shelter sprites in the `initiateShelters()` without position and visibility. After that, if the room needs a shelter duplicate, the duplicate could use one or more of the initiated sprites only by updating its position.

[Info   :Line Of Sight] Initiate Sprites. Entrance sprites length: 22
[Info   :Line Of Sight] Created Shelter Duplicate
[Info   :Line Of Sight] Initiate Sprites. Entrance sprites length: 16
[Info   :Line Of Sight] Initiate Sprites. Entrance sprites length: 22
[Info   :Line Of Sight] Initiate Sprites. Entrance sprites length: 16

###

Question: How many times is `CreateDuplicateShelterSprites()` called? And what triggers it?
If it's only called once, as stated by `shelterSpritesCreated` variable, then why is `duplicateShelterSprites` have so many items?

### `MoveToFront()` Code

Make sure it's on top
This one is important. 
If I were only to remove this and nothing else, the shadow sprite bug is still present but
the original duplicate sprite will get blocked.

Next step: Find out whether the shadow sprite is the same or different to its original
The original I mean the shelter sprite's duplicate in its own room
Find it out by going to a room with two screens. Load the shelter sprite. Go to the other screen.
Use screenpeek to see at the shelter sprite's original place.

Wait, is the bug for a room or a screen? Will the sprite reset after I go to other room or screen?
Note 0: I'm using wrap map and devtool to easily debug this
Note 1: The shadow sprite could be one or more (even three), by travelling from rooms to rooms with shelter sprites.
So far I haven't seen pattern.

```cs
duplicate.MoveToFront();
```

# `rCam.shortcutGraphics.entranceSprites` Theory

OORRRRRRR: Rain world stores all its loaded shelter sprites into `rCam.shortcutGraphics.entranceSprites`.

If not that, then my code accidentally stores every loaded shelter sprites into the memory.
If this is the case then:
I need to render duplicates ONLY for current room.

FALSE!! Debugger says the number of `rCam.shortcutGraphics.entranceSprites` items inside `DrawSprites()` is fixed and is based on current room.
Room A have 4 `entranceSprites`, B 32, C 20, etc.





the funny thing is, when "Total shelter sprites in container: 0", there is no shadow whatsoever, and the duplicate is still visible.



I ran couple trials long before this and saw that the sprites inside "shelterDuplicates" property is the ones that are visible outside FoV.



What if we only add a duplicate sprite ONCE into the container and then let "shelterDuplicates" handle the rest? We know that 

# New Bug

The duplicate sprite will only visible the first time you see it in the room. When you go to the other room and then come back to the room with the shelter sprite, the duplicate sprite will either disappeared or invisible outside FoV.

It seems that not only the `container`, but also the `shelterDuplicates` retains its previous value. I see it on debugger, the length of `shelterDuplicates` keeps increasing when I go back and forth rooms with shelter sprites.