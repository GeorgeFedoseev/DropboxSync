namespace DBXSync {
    public static class Endpoints {
        public static readonly string DOWNLOAD_FILE_ENDPOINT = "https://content.dropboxapi.com/2/files/download";
        public static readonly string METADATA_ENDPOINT = "https://api.dropboxapi.com/2/files/get_metadata";

        public static readonly string MOVE_ENDPOINT = "https://api.dropboxapi.com/2/files/move_v2";
        public static readonly string DELETE_ENDPOINT = "https://api.dropboxapi.com/2/files/delete_v2";

        public static readonly string LIST_FOLDER_ENDPOINT = "https://api.dropboxapi.com/2/files/list_folder";
		public static readonly string LIST_FOLDER_CONTINUE_ENDPOINT = "https://api.dropboxapi.com/2/files/list_folder/continue";
        public static readonly string LIST_FOLDER_LONGPOLL_ENDPOINT = "https://notify.dropboxapi.com/2/files/list_folder/longpoll";
		public static readonly string CREATE_FOLDER_ENDPOINT = "https://api.dropboxapi.com/2/files/create_folder_v2";

        // public static readonly string UPLOAD_FILE_ENDPOINT = "https://content.dropboxapi.com/2/files/upload";
        public static readonly string UPLOAD_START_ENDPOINT = "https://content.dropboxapi.com/2/files/upload_session/start";
        public static readonly string UPLOAD_APPEND_ENDPOINT = "https://content.dropboxapi.com/2/files/upload_session/append_v2";
        public static readonly string UPLOAD_FINISH_ENDPOINT = "https://content.dropboxapi.com/2/files/upload_session/finish";
    }
}