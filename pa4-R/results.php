<?php

// Database
$host = 'info344.cidwwjf4xkev.us-west-2.rds.amazonaws.com';
$port = '3306';
$dbname = 'info344';
$dsn = "mysql:host={$host};port={$port};dbname={$dbname}";
$username = 'info344user';
$password = 'coolcats';
$table = 'nba';

// Search query, sanitized so you can't just insert HTML
$search_param = filter_var($_GET['search'], FILTER_SANITIZE_STRING);

// Trim the search query, removing leading and ending whitespace and making all other whitespace just 1 space
$search = trim(preg_replace('/\s+/', ' ', $search_param));

// Get list of players:
try {
	$conn = new PDO($dsn, $username, $password);

	$conn->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

	include('player.php');

	$data = $conn -> prepare(
		"SELECT * FROM $table WHERE lower(name) = lower('$search')"
	);
	$data -> execute();


	// When the row is ready, push it to the rows array
	if($row = $data->fetch(PDO::FETCH_NUM)) {
		$player = new stdClass();
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

		echo $_GET['callback'] . '(' . json_encode($player) . ')';
	}
} catch(PDOException $e) {
    echo 'ERROR: ' . $e->getMessage();
}


?>
