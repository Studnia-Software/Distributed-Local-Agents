# Distributed-Local-Agents

## Uruchamianie:

### Serwer:
- Wymagany jest docker - wystarczy wejść do folderu z serwerem i wpisać ```docker compose up`

### Klient:
- Wymagany jest dotnet sdk - w folderze głównymz Psiruj.Client trzeba wpisać komendę ```dotnet run```
- Żeby spiąć klientów na innych maszynach z serwerem najlepiej jest mieć vpn typu tailscale, ze stałymi adresami IP.
- W aplikacjach klienckich trzeba ustawić IP hosta na to stałe z vpn-a

### Frontend:
- Wymagany jest node.js - w folderze frontend wystarczy wpisać ```npm i && npm run dev``` i wejść w link pokazany w konsoli
-  Frontend jest poglądowy - nasze rozwiązanie skupia się na udostępnieniu narzedzi to tworzenia rozproszonych systemów modelowych
