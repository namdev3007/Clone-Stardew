
    mergeInto(LibraryManager.library, {
     
      openWindow: function (url) {
        url = UTF8ToString(url);
        console.log('Opening link: ' + url);
        window.open(url,'_blank');
      }
    });
