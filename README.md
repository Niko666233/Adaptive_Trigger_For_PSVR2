# Battery Life Destroyer 9001: Energy Shortage!

For those who use PSVR2 but didn't know, you can use [PSVR2 Toolkit](https://github.com/BnuuySolutions/PSVR2Toolkit) to enable eye tracking and adaptive triggers etc on PC. There are already a couple of games (and mods) utilize those features so I think we should have this too.

Also it is recommended to use the next-custom-sync branch of PSVR2 Toolkit, which enables PCM haptics by default which is way better than the default "dumb" haptics. It also has custom tracking led sync which in theory can extend battery life. You can download the (currently) newest build from <https://github.com/BnuuySolutions/PSVR2Toolkit/actions/runs/21767048867> (iirc you'll need to log in to GitHub to download it).

## Features

- Clicky trigger effect
- Force feedback when firing, simulating recoil "kicks" (also vibration-based feedback as an alternative)
- Configurable (somewhat), reload the scene to reload the config

## Known Issues and Limitations

- You need to install [PSVR2 Toolkit](https://github.com/BnuuySolutions/PSVR2Toolkit) for this mod to function!
- My [![State-of-the-art Shitcode](https://img.shields.io/static/v1?label=State-of-the-art&message=Shitcode&color=7B5804)](https://github.com/trekhleb/state-of-the-art-shitcode) will make controllers' battery life drains considerably faster
- No per-weapon (type) configurations
- No proper dual stage trigger simulation currently
- Currently PSVR2 Toolkit has a bug that causes the left controller trigger effect to "stuck" and can't be removed. I've implemented a workaround but it's probably not perfect. Alternatively, you can disable the left controller trigger effect as a whole in the config file.

## I Asked Myself These Questions

### The mod doesn't work!?

- If you did install PSVR2 Toolkit properly, it's probably because the toolkit's next update will break the mod because of the IPC protocol changes. Please inform me if I didn't update the mod in time.

### It's really useless and glitchy

- Sorry, I'm not good at modding. If you have any suggestions, feel free tell me. (Discord: 妮可_Niko666, I'm in UTC+8 timezone so probably won't respond to messages in time)

### I want more

- I'll do my best.🫡

## Credits

- [PSVR2 Toolkit](https://github.com/BnuuySolutions/PSVR2Toolkit)
- [GTFO VR Plugin](https://github.com/uzugu/GTFO_VR_Plugin) where I stole a bunch of codes from
- [OpenScripts2](https://github.com/cityrobo/OpenScripts2) where I stole another bunch of codes from
- [H3VRPluginTemplate](https://github.com/H3VR-Modding/H3VRPluginTemplate)
- Everybody in H3VR community, especially Sora101Ven for the feedback
- You

[![ko-fi](https://storage.ko-fi.com/cdn/brandasset/v2/support_me_on_kofi_beige.png)](https://ko-fi.com/niko666233)
