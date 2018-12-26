// websocket gateway pada port 8070
const fs = require('fs');
const express = require('express');
const app = express();
const server = require('http').Server(app);
const io = require('socket.io')(server);
server.listen(8070);

// UDP server pada port 41181
const dgram = require('dgram');
const serverUDP = dgram.createSocket('udp4');
serverUDP.bind(41181); // server listening 0.0.0.0:41181

serverUDP.on('listening', function () {
    const address = serverUDP.address();
    console.log('UDP Server listening ' + address.address + ':' + address.port);
});

// menyediakan static files pada public
app.use(express.static('public'));

// semua route yang tidak dikenali akan di redirect ke page berikut
app.get('*', function (req, res) {
    var img = fs.readFileSync('public/images/Capture-404.png');
    res.writeHead(404, { 'Content-Type': 'image/png' });
    res.end(img, 'binary');
});


var mysocket = 0;
// ketika koneksi berhasil dibangun antara client dan server (termasuk udp server)
io.on('connection', function (socket) {
    if (mysocket == 0) {
        console.log(socket.id + ' has built connection');
        mysocket = socket;
    }
    else if (mysocket != socket && mysocket != 0) {
        console.log('client ' + socket.id + ' trying to built a connection, this client forced to disconnect');
        socket.emit('field', "Sedang Digunakan");
        socket.disconnect();
    }

    if (mysocket == socket) {
        // mengirim feedback (accepted command) kepada client (C# code process, ip loopback with port 11000)
        mysocket.on('feedbackCommand', function (data) {
            var feedbackCommand = Buffer.from(data);
            serverUDP.send(feedbackCommand, 11000);
        });

        mysocket.on('disconnect', function () {
            console.log(mysocket.id + ' = socket disconnected');
            mysocket = 0;
        });
    }
});

// emitting the message to all client (include who has emit it)
serverUDP.on('message', function (msg, rinfo) {
    console.log('msg : ' + msg);
    if (mysocket != 0) {
        mysocket.emit('field', "" + msg);

        var start_time = Date.now();
        mysocket.emit('delay', start_time);
    }
});