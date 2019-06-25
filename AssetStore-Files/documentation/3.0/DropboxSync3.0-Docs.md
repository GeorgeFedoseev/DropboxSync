# <a href="http://georgefedoseev.com/" style="text-decoration:none; color: black;"><img style="vertical-align:middle" src="http://georgefedoseev.com/dropbox-sync/assets/rounded-corners-122x128.png" width=50/>&nbsp;&nbsp;George Fedoseev</a>
# DropboxSync v3.0b1 Documentation
![Logo](http://georgefedoseev.com/dropbox-sync/assets/DropboxSync_Large.png?nocache=1)

### Contents
* Downloading files
  * [GetFileAsBytes()](#GetFileAsBytes())
  * [GetFile\<T>()](#GetFile\<T>())
  * [GetFileAsLocalCachedPath()](#GetFileAsLocalCachedPath())
* Uploading files

  * [UploadFile(localFilePath)](#UploadFile(localFilePath))

  * [UploadFile(bytes[])](#UploadFile(bytes[]))
* KeepSynced
  * 




# `Retrieving files`
## GetFileAsBytes()

```csharp
public void GetFileAsBytes(string dropboxPath,
                           Action<DropboxRequestResult<byte[]>> onResult,
                           Action<float> onProgress = null,
                           bool useCachedFirst = false,
                           bool useCachedIfOffline = true,
                           bool receiveUpdates = false)	
```
Asynchronously retrieves file from Dropbox as `byte[]`.  

### Parameters

Parameter | Description
--------- | ---
**dropboxPath** (`string`) | Path to file on Dropbox or inside of Dropbox App folder (depending on accessToken type). Should start with "/". *Example: /DropboxSyncExampleFolder/image.jpg*
**onResult** (`Action<DropboxRequestResult<byte[]>>`) | Callback function that receives `DropboxRequestResult<byte[]>` that contains result `data`, error `bool` indicator and `errorDescription` if there was any error.
**onProgress** (`Action<float> onProgress`) | Callback function that receives progress as float from 0 to 1.
**useCachedFirst** (`bool`) | Use cached version if it exists?
**useCachedIfOffline** (`bool`) | Use cached version if no Internet connection?
**receiveUpdates** (`bool`) | If `true`, then when there are remote updates on Dropbox, callback function `onResult ` will be triggered again with updated version of the file.

### Example usage
```csharp
// Download file from Dropbox (or used cached if no updates) as byte array.
DropboxSync.Main.GetFileAsBytes("/DropboxSyncExampleFolder/image.jpg", (res) => {
	if(res.error != null){
		Debug.LogError("Failed to get file bytes: "+res.error.ErrorDescription);
	}else{
		var imageBytes = res.data;
		Debug.Log("Got file as bytes array, length: "
									+imageBytes.Length.ToString()+" bytes");
	}
}, receiveUpdates:true);
```



## GetFile\<T>()

```csharp
public void GetFile<T>(string dropboxPath, 
                       Action<DropboxRequestResult<T>> onResult,
                       Action<float> onProgress = null,
                       bool useCachedFirst = false,
                       bool useCachedIfOffline = true,
                       bool receiveUpdates = false)

```
Asynchronously retrieves file from Dropbox and tries to produce object of specified type `T`.  

Supported generic types `T`:

- `string` - for text files
- `JsonObject`, `JsonArray` - for JSON-formatted files
- `Texture2D` - for image files

### Parameters

Parameter | Description
--------- | ---
**dropboxPath** (`string`) | Path to file on Dropbox or inside of Dropbox App folder (depending on accessToken type). Should start with "/". *Example: /DropboxSyncExampleFolder/image.jpg*
**onResult** (`Action<DropboxRequestResult<T>>`) | Callback function that receives `DropboxRequestResult<T>` that contains result `data`, error `bool` indicator and `errorDescription` if there was any error.
**onProgress** (`Action<float> onProgress`) | Callback function that receives progress as float from 0 to 1.
**useCachedFirst** (`bool`) | Use cached version if it exists?
**useCachedIfOffline** (`bool`) | Use cached version if no Internet connection?
**receiveUpdates** (`bool`) | If `true`, then when there are remote updates on Dropbox, callback function `onResult ` will be triggered again with updated version of the file.

### Example usage
```csharp
// Download Jpeg image from Dropbox (or used cached if no updates)
// and receive it as Texture2D object.
DropboxSync.Main.GetFile<Texture2D>("/DropboxSyncExampleFolder/image.jpg", (res) => {
	if(res.error != null){
		Debug.LogError("Error getting picture from Dropbox: "
                       				+res.error.ErrorDescription);
	}else{
		Debug.Log("Received picture from Dropbox!");
		var tex = res.data;
		UpdatePicture(tex);
	}
}, useCachedFirst:true);
```



## GetFileAsLocalCachedPath()

```csharp
public void GetFileAsLocalCachedPath(string dropboxPath,
									 Action<DropboxRequestResult<string>> onResult,
									 Action<float> onProgress = null,
									 bool useCachedFirst = false,
									 bool useCachedIfOffline = true,
									 bool receiveUpdates = false)
```
Asynchronously retrieves file from Dropbox and returns path to local filesystem cached copy.  

### Parameters

Parameter | Description
--------- | ---
**dropboxPath** (`string`) | Path to file on Dropbox or inside of Dropbox App folder (depending on accessToken type). Should start with "/". *Example: /DropboxSyncExampleFolder/image.jpg*
**onResult** (`Action<DropboxRequestResult<string>>`) | Callback function that receives `DropboxRequestResult<string>` that contains result `data`, error `bool` indicator and `errorDescription` if there was any error.
**onProgress** (`Action<float> onProgress`) | Callback function that receives progress as float from 0 to 1.
**useCachedFirst** (`bool`) | Serve cached version (if it exists) before event checking Dropbox for newer version? 
**useCachedIfOffline** (`bool`) | Use cached version if no Internet connection?
**receiveUpdates** (`bool`) | If `true`, then when there are remote updates on Dropbox, callback function `onResult ` will be triggered again with updated version of the file.

### Example usage
```csharp
// Download video from Dropbox (or use cached if no updates)
// and get local filepath.
DropboxSync.Main.GetFileAsLocalCachedPath("/DropboxSyncExampleFolder/video.mp4",
 (res) => {
	if(res.error != null){
		Debug.LogError("Error getting video from Dropbox: "
                       				+res.error.ErrorDescription);
	}else{
		Debug.Log("Received video from Dropbox!");
		var filePathInCache = res.data;
		PlayVideo(filePathInCache);
	}
}, receiveUpdates:true);

```



# `Uploading files`

## UploadFile(localFilePath)

```csharp
public void UploadFile(string dropboxPath,
         			   string localFilePath,
                       Action<DropboxRequestResult<DBXFile>> onResult,
                       Action<float> onProgress = null) 
```

Uploads file from specified filepath in local filesystem to Dropbox.

### Parameters

| Parameter                                              | Description                                                  |
| ------------------------------------------------------ | ------------------------------------------------------------ |
| **dropboxPath** (`string`)                             | Dropbox path where to upload file. *Example: /my_text.txt*   |
| **localFilePath** (`string`)                           | Full file path in local filesystem. *Example: C:/my_text.txt* |
| **onResult** (`Action<DropboxRequestResult<DBXFile>>`) | Result callback that receives created remote file metadata   |
| **onProgress** (`Action<float> onProgress`)            | Callback function that receives progress as float from 0 to 1. |

### Example usage

```csharp
DropboxSync.Main.UploadFile("/my_text.txt", "C:/my_text.txt", (res) => {
    if(res.error != null){				
        Debug.LogError("Error uploading file: "+res.error.ErrorDescription);
    }else{
        Debug.Log("File uploaded!");
    }
}, (progress) => {
    Debug.Log("Uploading file... "+Mathf.RoundToInt(progress*100)+"%");
});

```



## UploadFile(bytes[])

```csharp
public void UploadFile(string dropboxPath,
                       byte[] bytes,
                       Action<DropboxRequestResult<DBXFile>> onResult,
					   Action<float> onProgress = null) 
```

Uploads byte[] to specified Dropbox path.

### Parameters

| Parameter                                              | Description                                                  |
| ------------------------------------------------------ | ------------------------------------------------------------ |
| **dropboxPath** (`string`)                             | Dropbox path where to upload file. *Example: /my_text.txt*   |
| **bytes** (`byte[]`)                                   | Bytes array containing file data                             |
| **onResult** (`Action<DropboxRequestResult<DBXFile>>`) | Result callback that receives created remote file metadata   |
| **onProgress** (`Action<float> onProgress`)            | Callback function that receives progress as float from 0 to 1. |

### Example usage

```csharp
var bytes = Encoding.UTF8.GetBytes("my text");

DropboxSync.Main.UploadFile("/my_text.txt", bytes, (res) => {
    if(res.error != null){				
        Debug.LogError("Error uploading file: "+res.error.ErrorDescription);
    }else{
        Debug.Log("File uploaded!");
    }
}, (progress) => {
    Debug.Log("Uploading file... "+Mathf.RoundToInt(progress*100)+"%");
});

```


