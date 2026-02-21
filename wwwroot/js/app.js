/*
 * app.js - Клиентская часть чата (JavaScript)
 * 
 * Аналог в PHP: это JavaScript, который работает в браузере
 * Выполняет WebSocket подключение и обновляет DOM
 * 
 * Основные функции:
 * - Подключение к WebSocket серверу
 * - Отправка/получение сообщений
 * - Отображение сообщений в HTML
 */

/*
 * Ссылки на HTML элементы (аналог: $elements = document.querySelectorAll(...))
 * Вместо jQuery ($('#id')) используется нативный JavaScript
 */
const elements = {
    status: document.getElementById('status'),    // Статус соединения
    messages: document.getElementById('messages'), // Контейнер сообщений
    name: document.getElementById('name'),        // Поле ввода имени
    message: document.getElementById('message'),  // Поле ввода сообщения
    sendBtn: document.getElementById('sendBtn')   // Кнопка отправки
};

// Глобальные переменные
let ws = null;              // WebSocket соединение
let isConnected = false;     // Флаг подключения

/*
 * Инициализация при загрузке страницы
 * Аналог в PHP: $(document).ready(function() { ... });
 */
function init() {
    connect();               // Подключаемся к WebSocket
    setupEventListeners();   // Настраиваем обработчики событий
    loadUserName();          // Загружаем сохранённое имя
}

/*
 * Подключение к WebSocket серверу
 * 
 * Аналог в PHP (с Ratchet):
 * $loop = React\EventLoop\Loop::get();
 * $connector = new React\MySQL\Factory($loop);
 * $websocket = new React\Socket\Connector($loop);
 * 
 * WebSocket URL формируется автоматически на основе текущей страницы:
 * ws://localhost:5000/wss или wss://.../ws
 */
function connect() {
    // Определяем протокол (ws или wss для HTTPS)
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    
    // Формируем URL WebSocket сервера
    // Аналог: $wsUrl = 'ws://' . $_SERVER['HTTP_HOST'] . '/ws';
    const url = `${protocol}//${location.host}/ws`;
    
    console.log('Connecting to', url);
    
    // Создаём WebSocket соединение
    // Аналог в PHP: new Client($url);
    ws = new WebSocket(url);
    
    // Устанавливаем обработчики событий
    // Аналог: $ws->on('open', $onOpen);
    ws.onopen = onConnected;
    ws.onmessage = onMessage;
    ws.onclose = onDisconnected;
    ws.onerror = onError;
}

/*
 * Обработчик успешного подключения
 * Аналог: function onOpen(Event $e) { ... }
 */
function onConnected() {
    isConnected = true;
    updateStatus('Connected', true);
    addMessage('You joined the chat!', 'system');
}

/*
 * Обработчик входящих сообщений
 * Аналог: function onMessage(Message $msg) { ... }
 * 
 * @param event - объект события с данными от сервера
 */
function onMessage(event) {
    try {
        // Парсим JSON данные
        // Аналог в PHP: $data = json_decode($event->data, true);
        const data = JSON.parse(event.data);
        
        // Извлекаем данные (поддержка camelCase и PascalCase)
        // C# сериализует в camelCase, но на всякий случай проверяем оба варианта
        const name = data.name ?? data.Name ?? 'Anonymous';
        const text = data.text ?? data.Text ?? '';
        const type = data.type ?? data.Type ?? 'message';
        
        if (!text) return;
        
        // Формируем текст для отображения
        // Для системных сообщений только текст, для обычных - "Имя: текст"
        const displayText = type === 'system' 
            ? text 
            : `${name}: ${text}`;
            
        // Определяем, своё ли это сообщение
        // Сравниваем имя отправителя с текущим пользователем
        const isOwn = name === getCurrentUserName() || name === elements.name.value.trim();
        
        // Добавляем сообщение в чат
        addMessage(displayText, type, isOwn);
        
    } catch (err) {
        console.error('Parse error:', err);
    }
}

/*
 * Обработчик отключения
 * Аналог: function onClose(Event $e) { ... }
 */
function onDisconnected(event) {
    isConnected = false;
    updateStatus('Disconnected', false);
    
    // Показываем причину отключения, если есть
    const reason = event.reason ? ': ' + event.reason : '';
    addMessage(`Connection closed${reason}`, 'system');
    
    // Автоматическое переподключение через 3 секунды
    // Аналог: sleep(3); $this->connect();
    setTimeout(() => {
        if (!isConnected) {
            console.log('Attempting reconnect...');
            connect();
        }
    }, 3000);
}

/*
 * Обработчик ошибок
 * Аналог: function onError(Event $e) { ... }
 */
function onError(error) {
    console.error('WebSocket error:', error);
    addMessage('Connection error', 'system');
}

/*
 * Добавить сообщение в чат
 * 
 * @param text - текст сообщения
 * @param type - тип ('message' или 'system')
 * @param isOwn - флаг: своё сообщение или нет (для позиционирования слева/справа)
 * 
 * Аналог в PHP (генерирует HTML):
 * echo '<div class="message ' . $type . '">' . $text . '</div>';
 */
function addMessage(text, type, isOwn = false) {
    // Создаём новый элемент div
    const div = document.createElement('div');
    
    // Устанавливаем классы: message + type + own (если своё)
    // Аналог: $class = 'message ' . $type . ($isOwn ? ' own' : '');
    div.className = `message ${type} ${isOwn ? 'own' : ''}`;
    
    // Устанавливаем текстовое содержимое
    // Аналог: $div->textContent = $text;
    div.textContent = text;
    
    // Добавляем в контейнер сообщений
    // Аналог в PHP: echo $html;
    elements.messages.appendChild(div);
    
    // Прокручиваем к последнему сообщению
    // Аналог: $messages->scrollTop = $messages->scrollHeight;
    elements.messages.scrollTop = elements.messages.scrollHeight;
}

/*
 * Отправить сообщение на сервер
 * 
 * Аналог в PHP (Ratchet):
 * $connection->send(json_encode($data));
 */
function sendMessage() {
    // Проверяем подключение
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;
    
    // Получаем имя и текст из полей ввода
    // Аналог: $name = $_POST['name'] ?? 'Anonymous';
    const name = elements.name.value.trim() || 'Anonymous';
    const text = elements.message.value.trim();
    
    // Не отправляем пустые сообщения
    if (!text) return;
    
    // Формируем payload (аналог: $payload = ['name' => $name, 'text' => $text, 'type' => 'message'])
    const payload = { name, text, type: 'message' };
    
    // Добавляем сообщение локально (сразу показываем своё сообщение)
    addMessage(`${name}: ${text}`, 'message', true);
    
    // Отправляем на сервер
    // Аналог: $this->connection->send(json_encode($payload));
    ws.send(JSON.stringify(payload));
    
    // Очищаем поле ввода сообщения
    elements.message.value = '';
    
    // Фокус обратно на поле ввода
    elements.message.focus();
}

/*
 * Обновить статус соединения
 * 
 * @param text - текст статуса
 * @param connected - флаг подключения
 */
function updateStatus(text, connected) {
    elements.status.textContent = text;
    // Меняем класс в зависимости от статуса
    elements.status.className = `status ${connected ? 'connected' : 'disconnected'}`;
    
    // Блокируем/разблокируем поля ввода
    elements.message.disabled = !connected;
    elements.sendBtn.disabled = !connected;
}

/*
 * Настроить обработчики событий
 * 
 * Аналог в PHP (HTML): onclick="sendMessage()" или через JS
 */
function setupEventListeners() {
    // Клик по кнопке отправки
    elements.sendBtn.addEventListener('click', sendMessage);
    
    // Нажатие Enter в поле сообщения
    elements.message.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendMessage();
    });
    
    // Изменение имени пользователя
    elements.name.addEventListener('change', () => {
        saveUserName(elements.name.value.trim());
    });
}

/*
 * Получить имя текущего пользователя из localStorage
 * 
 * Аналог в PHP: $_SESSION['username'] ?? 'Anonymous';
 * localStorage похож на сессию, но хранится в браузере постоянно
 */
function getCurrentUserName() {
    return localStorage.getItem('chatUserName') || 'Anonymous';
}

/*
 * Сохранить имя пользователя в localStorage
 * 
 * Аналог в PHP: $_SESSION['username'] = $name;
 */
function saveUserName(name) {
    if (name) {
        localStorage.setItem('chatUserName', name);
        elements.name.value = name;
    }
}

/*
 * Загрузить сохранённое имя при старте
 * 
 * Аналог в PHP: 
 * if (isset($_SESSION['username'])) {
 *     $nameInput->value = $_SESSION['username'];
 * }
 */
function loadUserName() {
    const name = getCurrentUserName();
    if (name && name !== 'Anonymous') {
        elements.name.value = name;
    }   
}

// Запускаем инициализацию при загрузке DOM
// Аналог: $(document).ready(init);
document.addEventListener('DOMContentLoaded', init);

