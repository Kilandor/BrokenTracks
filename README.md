# BrokenTracks
BrokenTracks is a mod for Zeepkist to help better handle dealing with broken/updated tracks in playslists. To allow easier identification of these tracks, and some automation to keep the playlist going.

## Dependencies
ZeepSDK [Mod.io](https://mod.io/g/zeepkist/m/zeepsdk) - [Github](https://github.com/donderjoekel/ZeepSDK/)

## Features
- Build list of bad tracks when a track fails to load resulting in loading AO5.
- Color tracks on playlist to easily identify bad tracks <br />
  ![](https://zeepkist.kilandor.com/mods/brokentracks/images/playlist.png)
   - Skull/Red is a bad track
   - Question/Orange is a track that could be missing, unplayed, or unsubscirbed.
- Display warning to host when next track is known to be bad <br />
  ![](https://zeepkist.kilandor.com/mods/brokentracks/images/warning.png)
- Auto-skip AO5 when a track fails to load<br />
  ![](https://zeepkist.kilandor.com/mods/brokentracks/images/auto-skip.png)
- Auto-next when the next track in the playlist is known to be bad. Will choose the next known good track.
- Checks steam status to prevent marking or skipping anytime the status is not normal.
- Save/load bad tracks list to persist between games
- Fully configurable<br />
  ![](https://zeepkist.kilandor.com/mods/brokentracks/images/settings.png)
