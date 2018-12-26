
//var socket = io.connect('http://localhost:8070');
//const socket = io('http://localhost:8070'); // IP Localhost (Loopback)
const socket = io('http://192.168.1.3:8070'); // IP Home
//const socket = io('http://192.168.21.119:8070'); // IP LIPI
//const socket = io('http://192.168.0.15:8070'); // IP Kontrakan
//const socket = io('http://10.20.32.110:8070'); // IP Telkom University

socket.on('field', function (data) {
    console.log(data); // debugging command via HTML
    $("#field").html(data);
    
    var sound_id = '';
    var alert_icon = "info";
    var danger_mode = false;
    var alert_text = "Silahka pilih konfirmasi Anda";
    var alert_buttons = ["Maaf, Saya Sibuk", "Penuhi Kebutuhan"];

    switch (data) {
        case "Pasien Jatuh":
            sound_id = 'fall-detection';
            alert_icon = "warning";
            danger_mode = true;
            alert_text = "Segera Pergi Ke Ruangan Pasien!";
            alert_buttons = "OK!";
            break;
        case "Panggilan Darurat":
            sound_id = 'panggilan-darurat';
            break;
        case "Infus Habis":
            sound_id = 'infus-habis';
            break;
        case "Bantu Buang Hajat":
            sound_id = 'bantu-buang-hajat';
            break;
        case "Sedang Digunakan":
            swal({
                title: "Sedang Digunakan",
                text: "Sudah ada perawat lain yang sedang memantau pasien",
                icon: "info",
                buttons: false,
                closeOnClickOutside: false
            });
            $(".swal-overlay").css('background-color', 'rgba(0, 0, 0, 0.45)');
            break;
        default:
            swal("Perintah pasien tidak dikenali");
            break;
    }

    // Here is The test code for play sound
    swal({
        title: data,
        text: alert_text,
        buttons: alert_buttons,
        icon: alert_icon,
        dangerMode: danger_mode,
        closeOnClickOutside: false,
        closeOnEsc: false,
        onOpen: document.getElementById(sound_id).play()
    }).then((acceptCommand) => {
        if (acceptCommand == true) {
            socket.emit('feedbackCommand', data);
            document.getElementById('terima-kasih').play();
            swal({
                title: "Terima Kasih",
                text: "Tolong penuhi kebutuhan pasien dengan menerapkan 3S (senyum, salam, dan sapa)",
                icon: "success"
            });
        }
        else {
            socket.emit('feedbackCommand', "Sibuk");
            swal({
                title: "Semoga Lancar!",
                text: "Permintaan maaf anda akan kami sampaikan pada pasien. Mohon kembali lagi disaat waktu senggang",
                icon: "info"
            });
        }
    });

    if(data != "Pasien Jatuh") $(".swal-overlay").css('background-color', 'rgba(0, 0, 0, 0.45)');
    else $(".swal-overlay").css('background-color', 'rgba(200, 0, 0, 0.45)');

});

socket.on('delay', function (start_time) {
    var delay = Date.now() - start_time;
    console.log('Delay : ' + delay + 'ms');
});
