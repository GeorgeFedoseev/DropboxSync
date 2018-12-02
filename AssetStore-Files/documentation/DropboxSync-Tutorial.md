# <a href="/" style="text-decoration:none; color: black;"><img style="vertical-align:middle" src="http://georgefedoseev.com/dropbox-sync/assets/rounded-corners-122x128.png" width=50/>&nbsp;&nbsp;George Fedoseev</a>
--
# DropboxSync `Tutorial`<a href="/dropbox-sync/documentation.html">`Documentation`</a>
![Logo](http://georgefedoseev.com/dropbox-sync/assets/DropboxSync_Large.png?nocache=1)

### Contents
 
* [Setting up Dropbox App Folder](#SettingUpDropboxAppFolder)
* [Copying Example content for Example scenes](#CopyingExampleContent)
* [Running Example scenes](#RunningExampleScenes)
	
<br>
<hr>
<br>


## Getting started

<a name="SettingUpDropboxAppFolder"></a>
### Setting up Dropbox App Folder
#### Step 1
Navigate to <a href="https://www.dropbox.com/developers/apps/" target="_blank">Dropbox App creating page</a> and click **Create app** button.  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/1.png" width=600/>

#### Step 2
Create new app folder  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/2.png" width=500/>

#### Step 3
After creation you will be redirected to `https://www.dropbox.com/developers/apps/info/<your-app-key>`. Here you need to generate **accessToken** for your app that will be used by DropboxSync.  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/3.png" width=500/>

#### Step 4
Copy generated access token and paste into **DropboxSync Script** inspector field in **DropboxSyncFilesExample** in Unity.  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/4.png" width=500/>
<br>
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/5.png" width=500/>
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/6.png" width=300/>

Now you have example scene connected to your app folder. To run example scenes you need to copy example content to your created app folder. 

<a name="CopyingExampleContent"></a>
### Copying Example content for Example scenes
Save <a href="https://www.dropbox.com/sh/u9yubr1rcydaf9s/AAD5Sf2MTKVTMjZCX8A2t3oOa" target="_blank">this folder</a> to your Dropbox account **and then move it to created app folder** that you copied accessToken for on previous steps.

<a name="RunningExampleScenes"></a>
### Running Example scenes
#### Example scene 1 - DropboxSyncFilesExample
Now when you run **DropboxSyncFilesExample** scene you should see something like this:  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/7.png" width=500/>

#### Example scene 2 - DropboxSyncFoldersExample
To run other example scene (**DropboxSyncFoldersExample**) copy **accessToken** to DropboxSync inspector field same way and click play. You should see something like this:  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/8.png" width=500/>
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/9.png" width=500/>

### Custom scene
To use DropbBoxSync asset on your own scenes create GameObject and attach DropboxSync script to it. Then use asset from your scripts through `DropboxSync.Main` instance.
