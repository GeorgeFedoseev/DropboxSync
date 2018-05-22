using System;

[Serializable]
public class DBXFile : DBXItem {

    public string clientModified;
    public string serverModified;

    public string revision_id;
    public long filesize;
    public string contentHash;

    public DBXFile() {
        type = DBXItemType.File;

    }

    public static DBXFile FromDropboxJsonObject(JsonObject obj){
        
       return new DBXFile() {
           id = obj["id"] as string,
           name = obj["name"] as string,
           type = obj[".tag"].ToString() == "file" ? DBXItemType.File : DBXItemType.Folder,
           path = obj["path_lower"] as string,

           clientModified = obj["client_modified"] as string,
           serverModified = obj["server_modified"] as string,
           revision_id = obj["rev"] as string,
           filesize = long.Parse(obj["size"].ToString()),
           contentHash = obj["content_hash"] as string
       };
    }
}