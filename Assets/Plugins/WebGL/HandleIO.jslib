var HandleIO = {
    WindowAlert : function(message)
    {
        window.alert(UTF8ToString(message));
    },
    SyncFiles : function()
    {
        FS.syncfs(false,function (err) {
            // handle callback
        });
    }
};

mergeInto(LibraryManager.library, HandleIO);