# AcTools Utils

Some utils and obsolete libraries from [AcTools](https://github.com/gro-ove/actools/).

## Common libraries

- ### [AcTools.Kn5Render](https://github.com/gro-ove/actools-utils/tree/master/AcTools.Kn5Render)
    First, basic DX11 renderer. Basic forward rendering without any features. Code is very bad, almost everything in one class, but it does a lot of cool stuff apart from being a custom showroom: it generates [new ambient shadows](http://i.imgur.com/i4vsn0M.png) with a pretty neat quality, [new track maps](https://i2.wp.com/i.imgur.com/mjnn0Rr.png), finds livery colors. Also, of course, as a custom showroom it helps to draw new skins by [auto-reloading changed psd-files automatically](https://www.youtube.com/watch?v=-pGj1zKXgY0).

    [![AcTools Showroom](https://ascobash.files.wordpress.com/2015/10/uzmhnps.png?w=320)](https://ascobash.files.wordpress.com/2015/10/uzmhnps.png)
    
- ### [AcTools.Render.Deferred](https://github.com/gro-ove/actools-utils/tree/master/AcTools.Render.Deferred)
    Deferred rendering with dynamic lighting, dynamic shadows, HDR, tricky SSLR… Sadly, I couldn’t find a way to move all materials here correctly, so I decided to switch to forward rendering instead. Also, with forward, I can vary options on-fly, getting either very high-performance simple renderer (≈900 FPS) or pretty good looking one (≈60 FPS, without MSAA or higher pixel density).

    [![Custom Showroom](https://trello-attachments.s3.amazonaws.com/5717c5d2feb66091a673f1e8/1920x1080/237d1513a35509f5c48d969bdf4abd02/__custom_showroom_1461797524.jpg)](https://trello-attachments.s3.amazonaws.com/5717c5d2feb66091a673f1e8/1920x1080/237d1513a35509f5c48d969bdf4abd02/__custom_showroom_1461797524.jpg)
    
## Other projects

- ### [Kn5Materials](https://github.com/gro-ove/actools-utils/tree/master/Kn5Materials)
    Simple Windows Forms app to view/edit material properties of any kn5 file.

- ### [AiOptimizer](https://github.com/gro-ove/actools-utils/tree/master/AiOptimizer)
    Some attempt to optimize values from ai.ini using genetics mechanism.

- ### [DataAnalyzer](https://github.com/gro-ove/actools-utils/tree/master/DataAnalyzer)
    Analyzes car's data to find similar solutions using sets of rules.

- ### [DataCollector](https://github.com/gro-ove/actools-utils/tree/master/DataCollector)
    Collects specific fields from car's data to help to find some regularities.

- ### [AcToolsShowroom](https://github.com/gro-ove/actools-utils/tree/master/AcToolsShowroom)
    Just a small wrapper for AcTools.Kn5Render.

- ### [TrackMapGenerator](https://github.com/gro-ove/actools-utils/tree/master/TrackMapGenerator)
    Same, only a small wrapper.
