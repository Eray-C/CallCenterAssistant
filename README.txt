# CallCenterAssistant - Teknik Degerlendirme Calismasi

Bu proje, Atlas Yazilim Kisa Teknik Degerlendirme Calismasi kapsaminda hazirlanan; .NET 8 Web API, yerel yapay zeka (Ollama), cevrimdisi Speech-To-Text (Whisper), cevrimdisi Text-To-Speech (Piper), Asterisk PBX (WebRTC + SIP) entegrasyonu, Dockerizasyon ve FluentValidation ozelliklerini barindiran tam yigin (full-stack) bir cozumdur.

---

## Sistem Mimarisi



Servisler:
- Web API: ASP.NET Core 8.0 (Port: 8080)
- PBX: Asterisk (Docker) (Port: 5060/udp, 8088, 5038)
- AI (LLM): Ollama / llama3 (Host) (Port: 11434)
- STT: Whisper.net (ggml-base) (embedded)
- TTS: Piper TTS (tr_TR-fahrettin) (embedded)

---

## Tek Tikla Kolay Gorsel Kurulum (Onerilen)

Hicbir teknik bilgi gerekmez. Sadece asagidaki adimlari uygulayin.

### On Gereksinimler
Kurulum oncesinde bilgisayarinizda sunlarin yuklu ve calisir durumda olmasi gerekir:
- Docker Desktop: https://www.docker.com/products/docker-desktop/ (Kurulumdan sonra acik birakin)
- Ollama: https://ollama.com/ (Kurulumdan sonra "ollama pull llama3" calistirin)

### Kurulum Adimlari
1. RepoyuZIP olarak indirin:
   https://github.com/eray-c/CallCenterAssistant.git
   cd CallCenterAssistant

2. SetupGui'yi baslatin:
   dist\SetupGui.exe

3. SetupGui'de "Kurulumu Baslat" butonuna tiklayin:
   SetupGui otomatik olarak sunlari yapar:
   - Docker Desktop ve Ollama kontrolu yapar.
   - "llama3" modelini indirir ("ollama pull llama3").
   - "docker compose up -d --build" ile Asterisk PBX + API'yi birlikte derler ve baslatir.
   - Tum servisler hazir oldugunda bildirim gosterir.

4. Tamamlandi! Asagidaki adresler hazir:
   - Chatbot Arayuzu: http://localhost:8080
   - Swagger API Belgesi: http://localhost:8080/swagger/index.html
   - Asterisk WebSocket: ws://localhost:8088/ws
   - Asterisk AMI: localhost:5038

---

Servisleri Durdurmak:
- Servisleri durdur (imajlari koru):
  docker compose down
- Servisleri, imajlari ve volume'lari tamamen sil:
  docker compose down --rmi all --volumes

---

## Servis Yapilandirmasi

### Asterisk PBX
Config dosyalari "asterisk-config/etc/asterisk/" dizinindedir. Docker Compose bu dizini Asterisk konteynerine "/etc/asterisk" olarak mount eder.

Onemli dosyalar:
- pjsip.conf: SIP endpoint'leri (webrtc-user, 1001)
- pjsip_transport.conf: UDP, TCP, WebSocket transport ayarlari
- extensions.conf: Dialplan - hangi numaranin nereye gidecegi
- manager.conf: AMI kullanicisi (admin/password)
- http.conf: HTTP sunucusu (port 8088)
- rtp.conf: RTP port araligi (10000-10099)

WebRTC Kullanici Bilgileri (tarayici JsSIP icin):
- Kullanici adi: webrtc-user
- Sifre: webrtc1234
- SIP Server: localhost
- WS Port: 8088



Dahili Numaralar:
- 8000: Yapay Zeka AI Asistani

### Ortam Degiskenleri
Docker Compose'da su ortam degiskenlerini gecersiz kilabilirsiniz:
- Ollama__BaseUrl=http://host.docker.internal:11434
- Ollama__Model=llama3
- Asterisk__Host=host.docker.internal
- Asterisk__Port=5038
- Asterisk__Username=admin
- Asterisk__Password=password

---

## API Uc Noktalari

### 1. Sohbet API (Ollama)
POST /api/chat
Content-Type: application/json
{ "message": "Merhaba" }

Response:
{ "response": "Merhaba, size nasil yardimci olabilirim?" }

### 2. Sesi Metne Donusturme (STT)
POST /api/speech/transcribe
Content-Type: multipart/form-data
file: <16kHz Mono WAV>

### 3. Metni Sese Donusturme (TTS)
POST /api/speech/synthesize
Content-Type: application/json
{ "text": "Hos geldiniz." }
-> audio/wav stream doner.

### 4. Asterisk Arama Baslatma (AMI)
POST /api/asterisk/originate
Content-Type: application/json
{
  "channel": "PJSIP/webrtc-user",
  "exten": "8000",
  "context": "from-internal",
  "priority": 1,
  "callerId": "Asistan <1000>",
  "timeout": 30000
}

cURL Test Ornekleri:
- Sohbet testi:
  curl -X POST http://localhost:8080/api/chat -H "Content-Type: application/json" -d "{\"message\": \"Merhaba\"}"
- TTS testi:
  curl -X POST http://localhost:8080/api/speech/synthesize -H "Content-Type: application/json" -d "{\"text\": \"Hos geldiniz.\"}" --output ses.wav
- Asterisk AMI - Arama baslatma testi:
  curl -X POST http://localhost:8080/api/asterisk/originate -H "Content-Type: application/json" -d "{\"channel\":\"PJSIP/webrtc-user\",\"exten\":\"8000\",\"context\":\"from-internal\",\"priority\":1,\"callerId\":\"Test\",\"timeout\":30000}"

---

## Sorun Giderme

### Asterisk baslamiyor
- Asterisk loglarini kontrol et:
  docker compose logs asterisk
- Asterisk konteynerinde CLI ac:
  docker exec -it asterisk asterisk -rvvv

### API Asterisk'e baglanamiyor
- AMI baglantisini test et (telnet):
  telnet localhost 5038
  (Baglandiktan sonra: Asterisk Call Manager/... mesaji gormelisiniz)

### Ollama baglanti hatasi
- Ollama'nin host'ta calistigini dogrula:
  curl http://localhost:11434/api/tags
- Ollama'yi yeniden baslat:
  OLLAMA_HOST=0.0.0.0 ollama serve

### Port cakismasi
- 8088 portunu hangi surec kullaniyor?
  netstat -ano | findstr :8088

---



---
Atlas Yazilim Teknik Degerlendirme - 2026
