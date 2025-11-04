mkdir -p ./cert

openssl req -x509 -newkey rsa:4096 -keyout ./cert/server.key -out ./cert/server.crt -sha256 -days 3650 -nodes \
    -subj "/C=XX/ST=StateName/L=CityName/O=CompanyName/OU=CompanySectionName/CN=localhost"
