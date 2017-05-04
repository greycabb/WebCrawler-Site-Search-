// Part 1

// Variables for whether or not the trie is being built,
// since it's bad UX to make the user have to go to the webservice manually to create the trie
var buildingTrie = false;

// Simple loading animation cycle (. -> .. -> ...) for when/if the trie is being built
var CYCLE = ['   ', '.  ', '.. ', '...'];
var cycleProgress = 0;

var loadingTimer;

var typingTimer; // When the user types, there is a timer here that stops them from making too many requests at once

// Delay before user can make API query calls - whenever they type, there's a 0.2 second delay before the web method is called
function resetTypingTimer() {
    typingTimer = setTimeout(callWebMethod, 200);
}

// Add event listener to the search button
$(document).ready(function () {
    $('#search').keyup(function () {
        var keycode = event.keycode ? event.keyCode : event.which;
        if (keycode === 13) {
            goToPart2();
        } else {
            clearTimeout(typingTimer);
            resetTypingTimer();
        }
    });
    $('#searchbutton').click(function () {
        goToPart2();
    });
    $('#back').click(goToPart1);
    $('#fasterbutton').click(toggleSpeed);
});

var speedyQueries = false;

function toggleSpeed() {
    speedyQueries = !speedyQueries;
    if (!speedyQueries) {
        alert('Results will now appear more quickly, but only up to 10 will appear');
        $(this).html('Go faster');
    } else {
        $(this).html('Show more URLs');
    }
}

var part = 1;

function goToPart1() {
    $('#part1_querysuggestion').removeClass('hidden');
    $('#part2_resultspage').addClass('hidden');
    $('#search').val($('#search2').val());
    $('h1').html('Extremely High Speed Search Part 1');
    part = 1;
}

function goToPart2() {
    $('#part1_querysuggestion').addClass('hidden');
    $('#part2_resultspage').removeClass('hidden');
    $('#search2').val($('#search').val());
    $('h1').html('Extremely High Speed Search Part 2');
    queryCrawled();
    part = 2;
}

// Call the webmethod to get suggestions to show based on the value of the search bar
function callWebMethod() {

    if (buildingTrie) {
        return; // Don't spam the webservice with requests if the trie is not built yet
    }

    $('#levenshtein').empty();

    var textInput = $('#search').val();

    if (part === 2) {
        textInput = $('#search2').val();
    }

    if (textInput === '') {
        showResults(["Please enter a search query"], true, true);
        return;
    }

    // Query the trie
    try {
        $.ajax({
            type: "POST",
            url: "QuerySuggest.asmx/QueryTrie",
            data: JSON.stringify({ query: textInput }),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (msg) {
                console.log(msg.d);
                if (msg.d !== null) {
                    
                    
                    if (msg.d.length > 0) {
                        showResults(msg.d, false, false);
                        if (part === 2 && msg.d.length < 2 && msg.d[0] === $('#search2').val()) {
                            $('#resultsX').addClass('hidden');
                        } else {
                            $('#resultsX').removeClass('hidden');
                        }
                    } else {
                        showResults(["No results found for '" + textInput + "'"], true, true);
                    }
                } else {
                    // Trie doesn't exist, build it
                    autoBuildTrie();
                    loading();
                }
            },
            error: function (msg) {
                showResults(["An error occured while searching, try again"], true, true);
            }
        });
    } catch (err) {
        showResults([err], true, true);
    }
}

// Build the trie if it has not been built yet, automatically
// (since whenever the webserver resets, the stored trie is lost)
// This shouldn't ever have to run because of the keepalives but are useful just in case
// It's better than making the user go to the webservice and run it themselves!
function autoBuildTrie() {
    if (!buildingTrie) {
        buildingTrie = true;
        $.ajax({
            type: "POST",
            url: "QuerySuggest.asmx/BuildTrie",
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (msg) {
                buildingTrie = false;
                callWebMethod();
            },
            error: function (msg) {
                showResults(["Trie build failed"], true, true);
                clearTimeout(loading);
                buildingTrie = false;
            }
        });
    }
}

// Modifies the DOM to say it is in progress of building the trie,
// that way if for some reason the trie doesn't exist, the user isn't weirded out by the lack of feedback
function loading() {
    if (buildingTrie) {
        var results = $('#results, #resultsX');
        results.empty();

        results.html('Please wait - building trie structure ' + CYCLE[cycleProgress]); //+ ' ' + '<div><span id="triecounter"></span> | <span id="lasttitle"></span></div>');

        //t3_getTrieTitleCount();
        //t4_getLastTitleAddedToTrie();

        cycleProgress++;
        if (cycleProgress >= CYCLE.length) {
            cycleProgress = 0;
        }
        loadingTimer = setTimeout(loading, 800);
    }
}

// Show list of results from the query
function showResults(suggestionArray, escape, dull) {
    var results = $('#results, #resultsX');
    results.empty();

    suggestionArray.forEach(function (suggestion) {
        // Escape the characters, since if no results it writes what the user entered into the div
        if (escape) {
            suggestion = suggestion.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
        }
        if (suggestion.startsWith('No results found for')) {
            $('#resultsX').addClass('hidden');
        } else {
            $('#resultsX').removeClass('hidden');
        }
        var result = $('<div>').html(suggestion);
        if (!dull) {
            result.click(clickSearchSuggestion).addClass('hoverblue');
        } else {
            result.addClass('greyish');
        }

        results.append(result);
    });
}

// Onclick handler for the search suggestions, letting you click on them to search
function clickSearchSuggestion() {
    $('#search, #search2').val(this.innerHTML); // the text will be escaped
    callWebMethod(); // call the webmethod with the new input value

    goToPart2();
}

// Part 2

var typingTimer2;

function resetTypingTimer2() {
    typingTimer2 = setTimeout(queryCrawled, 800);
}

$(document).ready(function () {

    $('#search2').keyup(function (event) {
        var keycode = event.keycode ? event.keyCode : event.which;
        if (keycode === 13) {
            clearTimeout(typingTimer2);
            queryCrawled();
        } else {
            clearTimeout(typingTimer2);
            resetTypingTimer2();

            callWebMethod();
            clearTimeout(typingTimer);
            resetTypingTimer();
        }
    });

    $('#searchbutton2').click(queryCrawled);
});

var qc;

// Call title webmethod based on search input
function queryCrawled() {
    var textInput = $('#search2').val();
    console.log('text input: ' + textInput);

    var results = $('#results2');
    var stats = $('#playerstats');
    $(results).html("<i class='greyish'>Loading...</i>");
    $('#moreurls').html('');
    $(stats).html('');
    stats.addClass('hidden');

    if (textInput === '' || !textInput) {
        results.html('No search terms entered');
        return;
    }

    // Results component
    try {
        qc = $.ajax({
            type: "POST",
            url: "Admin.asmx/QueryCrawled",
            data: JSON.stringify({ query: textInput, limit: speedyQueries }),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (msg) {
                if (msg.d !== null) {
                    //console.log(msg.d.length);
                    appendSearchResults(msg.d, textInput);
                } else {
                    results.html("Search failed, try again");
                }
            },
            error: function (msg) {
                results.html("Search failed, try again");
                console.log(msg);
            }
        });
    } catch (err) {
        results.html("Search failed, try again");
    }

    // NBA Players component
    try {
        $.ajax({
            type: "POST",
            crossDomain: true,
            contentType: "application/json; charset=utf-8",
            url: "http://52.37.198.40/results.php",
            dataType: "JSONP",
            data: { search: textInput },

            success: function (data) {
                onDataReceived(data);
            }
        });
    } catch (err) {
        console.log('NBA player lookup failed for ' + textInput);
        console.log(err);
    }
}
function appendSearchResults(msgd, textInput) {
    var area = $('#results2');
    var area2 = $('#moreurls');
    area.html('');
    area2.html('');

    //console.log(msgd);
    
    var booped = false;

    msgd.forEach(function (entry) {
        
        if (entry[0] !== '$$' && entry[0]) {

            booped = true;

            // Div container
            var div = $('<div></div>');
            div.addClass('likeGoogle');

            // Title
            var title = $('<a target="_blank"></a>')
                .attr('href', entry[1])
                .html(entry[0])
                .addClass('googleTitle');


            title.click(function () {
                //console.log('boopx');
                rankUp(entry[1], textInput);
            });
            // EC: learn ranking based on user clicks on URLs

            // URL
            var url = $('<div></div>')
                .html(entry[1])
                .addClass('googleUrl');

            // Text
            var content = $('<div></div>')
                .html(' - ' + boldCertainWords(entry[3], textInput) + " ... ")
                .addClass('googleContent');

            // Date
            var date = $('<span></span>')
                .html(entry[2])
                .addClass('googleDate');

            content.prepend(date);

            // Assembling
            div.append(title).append(url).append(content);

            area.append(div);
        } else {

            var region = $('<div class="extraurlregion notclicked"></div>');

            var len = entry.length;

            var p1 = 11;

            entry.forEach(function (url, i) {
                var counter = i + 10;
                if (i > 0) {
                    var div = $('<a target="_blank"></a>').addClass('googleTitleAlt extraurl').html(url).attr('href', url).click(function () {
                        rankUp(url, textInput);
                    });

                    region.append(div);

                    if (counter % 10 === 0 || i === len - 1) {

                        p2 = counter;

                        region.prepend($('<h3></h3>').html(p1 + ' - ' + p2));

                        p1 = counter + 1;

                        area2.append(region);
                        region = $('<div class="extraurlregion"></div>').addClass('extraurlregion');
                    }
                }
            });
        }
    });
    if (msgd.length === 0 || !booped) {
        var escapedTextInput = textInput.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
        area.html("No articles found for '" + escapedTextInput + "'");
    }
}

function boldCertainWords(bodyText, query) {
    if (!bodyText) {
        return;
    }
    bodyText = bodyText.substring(0, 400);

    var words = query.split(' ');

    return bodyText.replace(new RegExp('(^|\\b)(' + words.join('|') + ')(\\b|$)', 'ig'), '$1<b>$2</b>$3');
}

function onDataReceived(data) {
    /*
	$player->name = $row[0];
	$player->team = $row[1];
	$player->gp = $row[2];
	$player->min = $row[3];
	$player->fg_m = $row[4];
	$player->fg_a = $row[5];
	$player->fg_pct = $row[6];
	$player->threept_m = $row[7];
	$player->threept_a = $row[8];
	$player->threept_pct = $row[9];
	$player->ft_m = $row[10];
	$player->ft_a = $row[11];
	$player->ft_pct = $row[12];
	$player->reb_off = $row[13];
	$player->reb_def = $row[14];
	$player->reb_tot = $row[15];
	$player->ast = $row[16];
	$player->to = $row[17];
	$player->stl = $row[18];
	$player->blk = $row[19];
	$player->pf = $row[20];
	$player->ppg = $row[21];
    */
    var area = $('#playerstats').html('');
    //var json = JSON.stringify(data);

    if (data['name'] !== undefined) {

        var n = data['name'];

        query = n + ' NBA';

        getPictures(query); // API call to Bing

        //var img = $('<img />')
        //    .attr('alt', n)
        //    .addClass('playerimage');

        //var portrait = $('<div></div>')
        //    .addClass('portrait')
        //    .append(img);

        var playername = $('<div></div>')
            .addClass('playername')
            .html(n);

        var playerteam = $('<small></small>')
            .addClass('playerteam')
            .html('Team: ' + data['team']);

        var nameandteam = $('<div></div>')
            .addClass('nameandteam')
            .append(playername)
            .append(playerteam);

        //area.append(portrait);
        area.append(nameandteam);

        

        var thead = $('<thead><tr><th></th><th></th></tr></thead>');

        var keys = Object.keys(data);
        var tbody = $('<tbody></tbody>');

        for (var i = 2; i < keys.length; i++) {
            var key = $('<td></td>').html(keys[i] + ": " );
            var value = $('<td></td>').html(data[keys[i]]);

            var tr = $('<tr></tr>').append(key).append(value);
            tbody.append(tr);
        }

        var table = $('<table></table>')
            .addClass('statstable')
            .append(thead)
            .append(tbody);

        area.append(table);
        

        area.removeClass('hidden').fadeIn(400);
    } else {
        area.addClass('hidden');
    }

    
}

// Learn user ranking based on clicks on URLs
function rankUp(urlRankUp, textInput) {
    console.log('clicked ' + urlRankUp + ' ' + textInput);
    try {
        $.ajax({
            type: "POST",
            url: "Admin.asmx/ClickUrl",
            data: JSON.stringify({ url: urlRankUp, decache: textInput }),
            contentType: "application/json; charset=utf-8",
            dataType: "json"
        });
    } catch (err) {
        console.log(err);
    }
}

// Get picture of player
function getPictures(query) {
    $('.playerstats').css('background-image', 'none');
    if (query === '' || !query) {
        return;
    }
    var params = {
        count: 1,
        safeSearch: 'Strict',
        q: query
    };

    $.ajax({
        url: "https://api.cognitive.microsoft.com/bing/v5.0/images/search?" + $.param(params),
        beforeSend: function (xhrObj) {
            // Request headers
            xhrObj.setRequestHeader("Content-Type", "multipart/form-data");
            xhrObj.setRequestHeader("Ocp-Apim-Subscription-Key", "API KEY GOES HERE"
        },
        type: "POST",
        // Request body
        data: "{body}"
    })
    .done(function (data) {
        var imageUrl = data['value'][0]['contentUrl'];
        $('.playerstats').css('background-image', 'url(' + imageUrl + ')');
    })
    .fail(function () {
        alert("error");
    });
}



// Get title count
function t3_getTrieTitleCount() {
    ajaxR('QuerySuggest.asmx/GetTitleCount', '#triecounter');
}
function t4_getLastTitleAddedToTrie() {
    ajaxR('QuerySuggest.asmx/GetLastTitleAdded', '#lasttitle');
}
var timerDict = {};
function ajaxR(URL, id, successText) {
    clearTimeout(timerDict[id]);

    var safety = false;
    if (URL === 'Admin.asmx/F1_ClearEverything') {
        $('div').addClass('safety');
        safety = true;

        setTimeout(function () {
            $('.safety').removeClass('safety');
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
                    $(id).html(msg.d);
                }
            }
        },
        error: function (msg) {
            results(id).html(msg);
        }
    });
}
