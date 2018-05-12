using System;

public class DBXFolder : DBXItem {    

    public DBXFolder() {
        type = DBXItemType.Folder;

    }

    public static DBXFolder FromDropboxJsonObject(JsonObject obj){
        
       return new DBXFolder() {
           id = obj["id"] as string,
           name = obj["name"] as string,
           type = obj[".tag"].ToString() == "file" ? DBXItemType.File : DBXItemType.Folder,
           path = obj["path_lower"] as string
       };
    }
}