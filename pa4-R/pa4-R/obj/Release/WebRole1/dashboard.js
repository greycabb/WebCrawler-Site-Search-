(function () {
    // Add event listener to everything
    $(document).ready(function () {
        $('#db_t1').find('button').click(t1_rebuildTrie);
        $('#db_t2').find('button').click(t2_clearCache);
        $('#db_t3').find('button').click(t3_getTrieTitleCount);
        $('#db_t4').find('button').click(t4_getLastTitleAddedToTrie);

        $('#db_c1').find('button').click(c1_crawlCnn);
        $('#db_c2').find('button').click(c2_crawlBleacherReport);
        $('#db_c3').find('button').click(c3_crawlCustomUrl);
        $('#db_c4').find('button').click(c4_getWebCrawlerState);
        $('#db_c5').find('button').click(c5_getCpuUtilization);
        $('#db_c6').find('button').click(c6_getRamAvailability);
        $('#db_c7').find('button').click(c7_getNumberOfUrlsCrawled);
        $('#db_c8').find('button').click(c8_getLast10UrlsCrawled);
        $('#db_c9').find('button').click(c9_showSizeOfQueue);
        $('#db_c10').find('button').click(c10_showSizeOfIndex);
        $('#db_c11').find('button').click(c11_showErrorsAndTheirUrls);
        $('#db_c12').find('button').click(c12_clearCache);
        $('#db_c13').find('button').click(c13_wipeEverything);

        setTimeout(updateAll(), 5000);

        $('#updateall').find('button').click(updateAll);
    });

    function updateAll() {
        t3_getTrieTitleCount();
        t4_getLastTitleAddedToTrie();
        c4_getWebCrawlerState();
        c5_getCpuUtilization();
        c6_getRamAvailability();
        c7_getNumberOfUrlsCrawled();
        c8_getLast10UrlsCrawled();
        c9_showSizeOfQueue();
        c10_showSizeOfIndex();
        //c11_showErrorsAndTheirUrls();
    }

    //_____________________________________
    // Trie stuff
    function t1_rebuildTrie() {
        results('#db_t1').html("Building trie...");
        ajaxR('QuerySuggest.asmx/BuildTrie', '#db_t1');
    }
    function t2_clearCache() {
        ajaxR('QuerySuggest.asmx/ClearCache', '#db_t2');
    }
    function t3_getTrieTitleCount() {
        ajaxR('QuerySuggest.asmx/GetTitleCount', '#db_t3');
    }
    function t4_getLastTitleAddedToTrie() {
        ajaxR('QuerySuggest.asmx/GetLastTitleAdded', '#db_t4');
    }

    //_____________________________________
    // Crawler stuff
    function c1_crawlCnn() {
        ajaxRWithParams('Admin.asmx/F2_StartCrawlingUrl', '#db_c1', 'http://www.cnn.com/robots.txt');
    }
    function c2_crawlBleacherReport() {
        ajaxRWithParams('Admin.asmx/F2_StartCrawlingUrl', '#db_c2', 'http://bleacherreport.com/robots.txt');
    }
    function c3_crawlCustomUrl() {
        var textInput = $('#search2').val();
        if (textInput === '') {
            results('#db_c3').html('needs input');
            return;
        }
        ajaxRWithParams('Admin.asmx/F2_StartCrawlingUrl', '#db_c3', textInput);
    }
    function c4_getWebCrawlerState() {
        results('#db_c4').html('loading...');
        ajaxR('Admin.asmx/M1_ShowWebCrawlerState', '#db_c4');
    }
    function c5_getCpuUtilization() {
        results('#db_c5').html('loading...');
        ajaxR('Admin.asmx/M2_ShowCpuUtilization', '#db_c5');
    }
    function c6_getRamAvailability() {
        results('#db_c6').html('loading...');
        ajaxR('Admin.asmx/M2_ShowRamAvailable', '#db_c6');
    }
    function c7_getNumberOfUrlsCrawled() {
        results('#db_c7').html('loading...');
        ajaxR('Admin.asmx/M3_ShowNumberOfUrlsCrawled', '#db_c7');
    }
    function c8_getLast10UrlsCrawled() {
        results('#db_c8').html('loading...');
        ajaxR('Admin.asmx/M4_ShowLast10UrlsCrawled', '#db_c8');
    }
    function c9_showSizeOfQueue() {
        results('#db_c9').html('loading...');
        ajaxR('Admin.asmx/M5_ShowSizeOfQueue', '#db_c9');
    }
    function c10_showSizeOfIndex() {
        results('#db_c10').html('loading...');
        ajaxR('Admin.asmx/M7_ShowSizeOfIndex', '#db_c10');
    }
    function c11_showErrorsAndTheirUrls() {
        results('#db_c11').html('loading...');
        ajaxR('Admin.asmx/M6_ShowErrorsAndTheirUrls', '#db_c11');
    }
    function c12_clearCache() {
        ajaxR('Admin.asmx/ClearCache', '#db_c12');
    }
    function c13_wipeEverything() {
        var password = $('#search3').val();
        if (password !== 'bites the dust') {
            results('#db_c13').html('invalid password');
        } else {
            results('#db_c13').html('Clearing everything... please wait 40 seconds');
            ajaxR('Admin.asmx/F1_ClearEverything', '#db_c13');
        }
    }

    //_____________________________________
    // Helpers
    var timerDict = {};

    // Ajax requests (no params)
    function ajaxR(URL, id, successText) {
        clearTimeout(timerDict[id]);

        var safety = false;
        if (URL === 'Admin.asmx/F1_ClearEverything') {
            $('div').addClass('safety');
            safety = true;

            setTimeout(function () {
                $('.safety').removeClass('safety');
                setTimeout(updateAll(), 5000);
            }, 40000);
        }
        $.ajax({
            type: "POST",
            url: URL,
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (msg) {
                if (successText) {
                    
                    results(id).html(successText);
                } else {
                    if (msg.d !== null) {
                        if (!Array.isArray(msg.d)) {
                            results(id).html(msg.d);
                        } else {
                            var res = results(id);
                            res.html('');
                            msg.d.forEach(function (str) {
                                var div = $('<div></div>');
                                div.html(str);
                                res.append(div);
                            });
                        }
                    }
                    else {
                        //results(id).html('trying again in 5 seconds');
                        timerDict[id] = setTimeout(function () { ajaxR(URL, id); }, 6000);
                    }
                }
            },
            error: function (msg) {
                results(id).html(msg);
            }
        });
    }
    // Ajax requests (with params)
    function ajaxRWithParams(URL, id, input) {
        
        $.ajax({
            type: "POST",
            url: URL,
            data: JSON.stringify({ query: input }),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (msg) {
                results(id).html(msg.d);
            },
            error: function (msg) {
                results(id).html(msg);
            }
        });
    }
    // Gets results div inside the specified ID
    function results(id) {
        return $(id).find('.results');
    }

})();