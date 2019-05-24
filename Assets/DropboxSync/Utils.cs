

namespace DBXSync {

    public static class Utils {

        public static bool IsAccessTokenValid(string accessToken) {
            if(accessToken == null) {
                return false;
            }
            
            if(accessToken.Trim().Length == 0){
                return false;
            }

            if(accessToken.Length < 20){
                return false;
            }

            return true;
        }


        public static string FixDropboxJSONString(string jsonStr) {
            
            jsonStr = jsonStr.Replace("\".tag\"", "\"tag\"");

            return jsonStr;
        }

    }

}