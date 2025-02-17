# irHub changelogs

## Version 1.0.1
This version brings a small bugfix

Changes:
- Fixed an issue where update dialog wouldn't show in the center of the screen

## Version 1.0.0
This version brings a lot of bugfixes, new features, quality of life improvements, UI updates and performance improvements.

Changes:
- Added the option to start the app minimized
- Added an optional garage cover for streamers
- Added an update dialog with detailed release notes
- Added logging which will help track down and tackle bugs and other issues. Logs are saved in documents/irHub/logs
- Adding another instance of the app now focusses the already open app (and recovers from system tray if needed) 
- Save button now only shows when there is something to save when editing an application
- Moved the start all and stop all buttons to the iracing banner
- Changed banner colour to a gradient
- Custom app icons are now copied to the irHub directory to prevent the image from resetting if the file is moved

Performance:
- Decreased memory utilization in the code that loads the applications' icons
- Application checks to see if they are running now happen all at once, decreasing overhead
- Upgraded to .NET 9
- Reworked a piece of code that had boxing allocation

Bugs:
- Fixed a bug where the app could crash when stopping an application
- Fixed an issue where an application could wrongfully get the Not Found status
- Fixed a bug where apps wouldn't start after starting the iRacing sim
- Fixed an issue where the app would crash when you added an app that was already running

## Version 0.9.10
This version is a small improvement in terms of performance and has some QoL updates and has a few bugfixes.

## Version 0.9.9

This version brings a basic auto update system which will later be expanded upon to be more detailed.

It is recommended to install this new version, so you always stay up to date with the latest features!

## Version 0.9.0

I am glad to announce we finally have a workable version ready for release!

In this version you'll be able to add your favorite iRacing tools and let them automatically start whenever you start playing iRacing!

Note that there are likely still some bugs; you can report them [here](https://github.com/Marijn17s/irHub/issues/new/choose)

There is still a lot to be done, and I'm excited to see what we can make in the future.

> SHA256: 492C74A6333C535C1FFE904A12759C7D8A535DBA899523EE3EF4DA87DC03CC41