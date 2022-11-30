namespace DBXSync {

	[System.Serializable]
    public class PathParameters : RequestParameters {
        public string path;			

        public PathParameters() {}
        
        public PathParameters(string path){
            this.path = path;
        }
    }

}