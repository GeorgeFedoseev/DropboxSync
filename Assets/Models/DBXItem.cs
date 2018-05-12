using System;

[Serializable]
public enum DBXItemType {
    File,
    Folder
}

[Serializable]
public class DBXItem {
    public string id;
    public string name;
    public DBXItemType type;
    public string path;


    public static DBXItem FromJsonObject(JsonObject obj){
        //var obj = SimpleJson.DeserializeObject(json);
       // Debug.Log((((root as JsonObject)["entries"] as JsonArray)[0] as JsonObject)[".tag"] as string);
       return new DBXItem() {
           id = obj["id"] as string,
           name = obj["name"] as string,
           type = obj[".tag"].ToString() == "file" ? DBXItemType.File : DBXItemType.Folder,
           path = obj["path_lower"] as string
       };
    }
}