<?php
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, PUT, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

$body = file_get_contents('php://input');
if ($body && strpos(trim($body), '{') === 0) {
    file_put_contents(__DIR__ . '/denah.json', $body);
    header('Content-Type: application/json');
    echo json_encode(['status' => 'ok', 'saved' => true]);
} else {
    http_response_code(400);
    header('Content-Type: application/json');
    echo json_encode(['status' => 'error', 'message' => 'Invalid JSON']);
}
?>
