import http.server
import socketserver
import json
from datetime import datetime

class ChatHandler(http.server.SimpleHTTPRequestHandler):
    messages = []
    users = set()

    def log_message(self, format, *args):
        pass # Disable HTTP request logging
    
    def do_GET(self):
        routes = {'/': '/chat.html', '/messages': self.send_messages, '/users': self.send_users}
        if self.path in routes:
            if callable(routes[self.path]):
                routes[self.path]()
                return
            self.path = routes[self.path]
        return super().do_GET()
    
    def do_POST(self):
        handlers = {'/send': self.handle_send_message, '/join': self.handle_join, '/leave': self.handle_leave}
        if self.path in handlers:
            handlers[self.path]()
    
    def get_post_data(self):
        content_length = int(self.headers['Content-Length'])
        return json.loads(self.rfile.read(content_length).decode('utf-8'))
    
    def add_system_message(self, message):
        self.messages.append({
            'username': 'System',
            'message': message,
            'timestamp': datetime.now().strftime('%H:%M:%S'),
            'system': True
        })
    
    def handle_send_message(self):
        data = self.get_post_data()
        self.messages.append({
            'username': data['username'],
            'message': data['message'],
            'timestamp': datetime.now().strftime('%H:%M:%S')
        })
        if len(self.messages) > 100:
            self.messages = self.messages[-100:]
        self.send_json_response({'status': 'success'})
    
    def handle_join(self):
        data = self.get_post_data()
        username = data['username']
        self.users.add(username)
        self.add_system_message(f'{username} joined the chat')
        self.send_json_response({'status': 'success'})
    
    def handle_leave(self):
        data = self.get_post_data()
        username = data['username']
        self.users.discard(username)
        self.add_system_message(f'{username} left the chat')
        self.send_json_response({'status': 'success'})
    
    def send_messages(self):
        self.send_json_response(self.messages)
    
    def send_users(self):
        self.send_json_response(list(self.users))
    
    def send_json_response(self, data):
        self.send_response(200)
        self.send_header('Content-type', 'application/json')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(json.dumps(data, ensure_ascii=False).encode('utf-8'))

HTML_CONTENT = '''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>EchoVerse</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            padding: 20px; 
            background: #f5f5f5;
        }
        .container { 
            max-width: 850px; 
            margin: 0 auto; 
            background: white; 
            border-radius: 8px; 
            padding: 20px; 
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }
        .header { text-align: center; margin-bottom: 20px; }
        h1 { color: #333; margin-bottom: 15px; font-size: 24px; }
        #loginArea { display: flex; gap: 10px; max-width: 400px; margin: 0 auto; }
        #chatControls span { margin-right: 10px; }
        #chatRoom { margin-top: 20px; }
        .chat-area { display: flex; gap: 20px; }
        .messages { 
            flex: 1; 
            height: 420px; 
            padding: 12px; 
            overflow-y: auto; 
            background: #fafafa;
            border-radius: 6px;
            border: 1px solid #e0e0e0;
        }
        .messages::-webkit-scrollbar { width: 8px; }
        .messages::-webkit-scrollbar-track { background: #f1f1f1; }
        .messages::-webkit-scrollbar-thumb { background: #ccc; border-radius: 4px; }
        .messages::-webkit-scrollbar-thumb:hover { background: #aaa; }
        .users { 
            width: 160px; 
            padding: 12px; 
            background: #fafafa;
            border-radius: 6px;
            border: 1px solid #e0e0e0;
        }
        .users h4 { margin-bottom: 10px; color: #555; font-size: 13px; }
        .message { 
            margin-bottom: 10px; 
            padding: 10px; 
            background: white; 
            border-radius: 6px;
            border: 1px solid #eee;
        }
        .message strong { color: #007bff; }
        .message small { color: #888; font-size: 11px; }
        .system { background: #e7f3ff; font-style: italic; border-color: #b3d9ff; }
        .system strong { color: #0066cc; }
        .input-area { margin-top: 20px; display: flex; gap: 10px; }
        input { 
            flex: 1; 
            padding: 10px 14px; 
            border: 1px solid #ddd; 
            border-radius: 6px; 
            font-size: 14px;
            font-family: inherit;
        }
        input:focus { outline: none; border-color: #007bff; }
        button { 
            padding: 10px 24px; 
            background: #007bff;
            color: white; 
            border: none; 
            border-radius: 6px; 
            cursor: pointer; 
            font-size: 14px;
            font-family: inherit;
        }
        button:hover { background: #0056b3; }
        .user-item { 
            padding: 6px 10px; 
            margin: 4px 0; 
            background: white;
            border-radius: 4px;
            font-size: 13px;
            border: 1px solid #eee;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <div id="loginArea">
                <input type="text" id="usernameInput" placeholder="Enter your nickname" maxlength="20">
                <button onclick="joinChat()">Join</button>
            </div>
            <div id="chatControls" style="display:none;">
                <span>Welcome, <strong id="currentUser"></strong>!</span>
                <button onclick="leaveChat()">Leave</button>
            </div>
        </div>
        <div id="chatRoom" style="display:none;">
            <div class="chat-area">
                <div class="messages" id="messages"></div>
                <div class="users">
                    <h4>Online Users</h4>
                    <div id="usersList"></div>
                </div>
            </div>
            <div class="input-area">
                <input type="text" id="messageInput" placeholder="Type your message..." maxlength="500">
                <button onclick="sendMessage()">Send</button>
            </div>
        </div>
    </div>
    <script>
        let currentUsername = '', messageInterval;
        let shouldAutoScroll = true;
        
        function joinChat() {
            const username = document.getElementById('usernameInput').value.trim();
            if (!username) return alert('Please enter a nickname');
            
            fetch('/join', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({username})
            }).then(() => {
                currentUsername = username;
                document.getElementById('loginArea').style.display = 'none';
                document.getElementById('chatControls').style.display = 'block';
                document.getElementById('chatRoom').style.display = 'block';
                document.getElementById('currentUser').textContent = username;
                messageInterval = setInterval(loadMessages, 1000);
                loadMessages();
                loadUsers();
                
                const messagesDiv = document.getElementById('messages');
                messagesDiv.addEventListener('scroll', function() {
                    const isNearBottom = messagesDiv.scrollTop + messagesDiv.clientHeight >= messagesDiv.scrollHeight - 50;
                    shouldAutoScroll = isNearBottom;
                });
            });
        }
        
        function leaveChat() {
            fetch('/leave', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({username: currentUsername})
            }).then(() => {
                clearInterval(messageInterval);
                document.getElementById('loginArea').style.display = 'block';
                document.getElementById('chatControls').style.display = 'none';
                document.getElementById('chatRoom').style.display = 'none';
                document.getElementById('messages').innerHTML = '';
                currentUsername = '';
                shouldAutoScroll = true;
            });
        }
        
        function sendMessage() {
            const input = document.getElementById('messageInput');
            const message = input.value.trim();
            if (!message) return;
            
            fetch('/send', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({username: currentUsername, message})
            }).then(() => {
                input.value = '';
                shouldAutoScroll = true;
                loadMessages();
            });
        }
        
        function loadMessages() {
            fetch('/messages')
                .then(r => r.json())
                .then(messages => {
                    const div = document.getElementById('messages');
                    div.innerHTML = messages.map(msg => 
                        `<div class="message ${msg.system ? 'system' : ''}">
                            <strong>${msg.username}</strong> <small>[${msg.timestamp}]</small><br>${msg.message}
                        </div>`
                    ).join('');
                    
                    if (shouldAutoScroll) {
                        div.scrollTop = div.scrollHeight;
                    }
                });
        }
        
        function loadUsers() {
            fetch('/users')
                .then(r => r.json())
                .then(users => {
                    document.getElementById('usersList').innerHTML = users.map(u => 
                        `<div class="user-item">${u}</div>`
                    ).join('');
                });
        }
        
        document.getElementById('messageInput').addEventListener('keypress', e => e.key === 'Enter' && sendMessage());
        document.getElementById('usernameInput').addEventListener('keypress', e => e.key === 'Enter' && joinChat());
        setInterval(loadUsers, 5000);
    </script>
</body>
</html>'''

with open('chat.html', 'w', encoding='utf-8') as f:
    f.write(HTML_CONTENT)

if __name__ == "__main__":
    PORT = 8000
    with socketserver.TCPServer(("", PORT), ChatHandler) as httpd:
        print(f"Chat server running on port {PORT}")
        print(f"Visit http://localhost:{PORT} or http://[your-ip]:{PORT}")
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nServer stopped")
