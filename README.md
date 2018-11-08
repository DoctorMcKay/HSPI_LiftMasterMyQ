# LiftMaster MyQ for HomeSeer HS3

This is a free, open-source plugin to control LiftMaster-branded MyQ garage doors and gates. It may also work with other
brands of MyQ openers, but has only been tested on a LiftMaster 8355W opener.

It should work with any opener that can be controlled by the
[MyQ Garage & Access Control app](https://play.google.com/store/apps/details?id=com.chamberlain.android.liftmaster.myq).

# Installation

You will need to install the plugin manually.
Download HSPI_LiftMasterMyQ.exe from the [latest release](https://github.com/DoctorMcKay/HSPI_LiftMasterMyQ/releases/latest)
and drop it into your HS3 directory (where HomeSeerAPI.dll is located). Then restart HS3.

**Make sure you don't change the filename or the plugin won't work.** No additional DLLs are required.

# Configuration

Enable the plugin from the `Plug-Ins > Manage` page. Once enabled, it should give an error indicating that your MyQ
username and password haven't yet been set. Click on the plugin name or use `Plug-Ins > LiftMaster MyQ > Settings`
to open the settings page, where you should enter your MyQ email address and password.

Optionally you can change the poll frequency, which is how frequently the plugin queries the MyQ server for your garage
door status. This defaults to 10,000 ms (10 seconds) but can be set as low as 5,000 ms (5 seconds). The lower this value,
the more quickly your door status will update in the HS3 UI (and trigger events), but the more load you place on the MyQ
server and the more likely you draw their operations' guys attention. We aren't exactly using an API that's publicly
supported so if you draw their attention, you might get cut off from the service. Lower at your own risk.

Once you enter your email, password, and optionally change the poll frequency, click "Submit". The plugin will
immediately attempt to authenticate with MyQ and will let you know whether it succeeded or failed.

Once you successfully authenticate, the plugin will automatically create devices in HS3 for each garage door or gate
registered in your MyQ account. New openers that you register in MyQ will have devices created for them in HS3
immediately and automatically.

If you delete a device from your MyQ account, you will need to manually delete its associated device in HS3. If you
delete a device from HS3 but not from MyQ, it will be re-created automatically the next time the plugin starts.

# Updating Your MyQ Password

If you change your MyQ password, you will need to update it on the plugin's settings page. Once a password is submitted
and saved, the password box will be pre-filled with "*****". To update your MyQ password in the plugin, clear the
password box completely then enter your new password.

# Software Support

This plugin is tested and works under:

- Windows (Windows 10 version 1803)
- Linux (Raspbian 9 Stretch)
- Mono version 4.6.2
