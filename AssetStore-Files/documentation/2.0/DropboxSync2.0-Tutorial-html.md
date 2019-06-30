# <a href="http://georgefedoseev.com/" style="text-decoration:none; color: black;"><img style="vertical-align:middle" src="http://georgefedoseev.com/dropbox-sync/assets/rounded-corners-122x128.png" width=50/>&nbsp;&nbsp;George Fedoseev</a>

# DropboxSync v2.0 

# `Tutorial` <a href="/dropbox-sync/documentation.html">`Documentation`</a>

![Logo](http://georgefedoseev.com/dropbox-sync/assets/DropboxSync_Large.png?nocache=1)

### Contents

* [Setting up Dropbox App Folder](#Setting up Dropbox App Folder)
* [Copying Example content for Example scenes](#Copying Example content for Example scenes)
* [Running Example scenes](#Running Example scenes)
  * [Example scene 1 - DownloadExample](#Example scene 1 - DownloadExample)
  * [Example scene 2 - FileExplorerExample](#Example scene 2 - FileExplorerExample)
  * [Example scene 3 - UploadFileExample](#Example scene 3 - UploadFileExample)
  * [Example scene 4 - UploadTextExample](#Example scene 4 - UploadTextExample)
  * [Example scene 5 -  TestMainMethods](#Example scene 5 -  TestMainMethods)
* [Setting up Custom Scene](#Setting up Custom Scene)



## Getting started

### Setting up Dropbox App Folder
#### Step 1
Navigate to <a href="https://www.dropbox.com/developers/apps/" target="_blank">Dropbox App creating page</a> and click **Create app** button.  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/1.png" width=600/>

#### Step 2
Create new app folder  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/2.png" width=500/>

#### Step 3
After creation you will be redirected to `https://www.dropbox.com/developers/apps/info/<your-app-key>`. Here you need to generate **accessToken** for your app that will be used by DropboxSync.  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/3.png" width=700/>

#### Step 4
Copy generated access token and paste into **DropboxSync Script** inspector field in **DownloadExample**  scene in Unity.  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/4.png" width=800/>





<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/DropboxSync_gameobject.PNG" width=500/>



<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/DropboxSync_inspector_access_token.PNG" width=400/>

Now you have example scene connected to your app folder. To run example scenes you need to copy example content to your created app folder. 



### Copying Example content for Example scenes
Save <a href="https://www.dropbox.com/sh/u9yubr1rcydaf9s/AAD5Sf2MTKVTMjZCX8A2t3oOa" target="_blank">this folder</a> to your Dropbox account **and then move it to created app folder** that you copied accessToken for on previous steps.



### Running Example scenes
#### Example scene 1 - DownloadExample
Now when you run **DownloadExample** scene you should see something like this:  
<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/DownloadExample.PNG" width=800/>



#### Example scene 2 - FileExplorerExample

To run other example scene (**FileExplorerExample**) copy **accessToken** to DropboxSync inspector field same way and click play. You should see something like this:

<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/FileExplorerExample.PNG" width=800/>



#### Example scene 3 - UploadFileExample

This scene demonstrates ability to upload file to Dropbox from local filesystem. Same as with previous scenes - **don't forget to input your valid accessToken** into DropboxSync script. 

<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/UploadFileExample.PNG" width=800>



#### Example scene 4 - UploadTextExample

This scene demonstrates uploading byte array of text to Dropbox as a text file.

<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/UploadTextExample.PNG" width=800>



#### Example scene 5 -  TestMainMethods

This scene allows to test all main methods like uploading, downloading, moving and deleting in one run. **Don't forget to input valid accessToken** or you will get Bad Request error.

<img src="http://georgefedoseev.com/dropbox-sync/assets/tutorial-assets/TestMainMethods.PNG" width=800>



### Setting up Custom Scene
To use DropboxSync asset in your own scenes create GameObject and attach DropboxSync script to it. Then use asset from your scripts through `DropboxSync.Main` instance.
