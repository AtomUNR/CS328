BATTLE TOWER — INTERACTIVE TITLE SCREEN KIT

Includes:
- BattleTowerTitleScreen.cs: Begin zoom/scene load, settings panel, master volume, quit, optional button sound.
- TitleChoiceHighlight.cs: shows a linked tower-floor light when a menu Button is selected or hovered.
- CloudDrift.cs: slowly moves a separate cloud layer back and forth.

ADD THE SCRIPTS
1. Extract this folder and copy the three .cs files into your Unity project's Assets folder.
2. In the title scene, create an empty GameObject named TitleScreenController and add BattleTowerTitleScreen.
3. Set First Game Scene to the exact name of your first gameplay scene. Add both title and gameplay scenes in File > Build Settings > Scenes In Build.
4. Assign the Settings Panel GameObject. Keep it disabled at scene start; the script also hides it when it awakens.
5. On the Begin Button's Button > On Click event, add TitleScreenController and choose BattleTowerTitleScreen.BeginGame.
6. On Settings, choose OpenSettings. On the panel's close button, choose CloseSettings. On Quit, choose QuitGame.

ZOOM INTO THE TOWER
1. Assign the full-screen title background Image's RectTransform to Background To Zoom.
2. In that RectTransform, set Pivot to the normalized position of the tower doorway within the image (0,0 is bottom-left; 1,1 is top-right). The doorway stays anchored while the background scales toward it.
3. Adjust Zoom Scale and Transition Duration in the Inspector. If your background is split into layers, use a full-screen parent RectTransform for the image layers.

TOWER LIGHTS FOR MENU CHOICES
1. Create one small glow sprite/GameObject for each tower floor you want to illuminate. Place these on the background, outside the menu button hierarchy.
2. Disable the glow objects at scene start.
3. Add TitleChoiceHighlight to each menu Button and assign its corresponding floor glow.
4. The same effect works with mouse hover and EventSystem keyboard/controller selection. Keep glow graphics' Raycast Target disabled so they do not block pointer input.
5. Give each Button a visible selected/hover transition (for example Color Tint) so the menu choice itself is easy to see.

DRIFTING CLOUDS
1. Put the clouds on a separate sprite/layer from the tower and menu.
2. Add CloudDrift to the cloud GameObject and tune Travel Distance and Cycles Per Second. Keep the movement subtle; adjust distance to suit your scene's scale.

OPTIONAL CLICK SOUND AND VOLUME
- Assign an AudioSource and click clip on the controller to play a sound for menu actions.
- To connect a Settings Slider, add the controller to its On Value Changed event and choose SetMasterVolume. Configure the slider from 0 to 1.

NOTES
- The zoom is optional: leave Background To Zoom empty to wait for the transition duration and then load the scene.
- Make sure EventSystem exists in the title scene for UI selection and pointer events.
- The Quit button exits a built player. Application.Quit does not close Play Mode in the Unity Editor.
