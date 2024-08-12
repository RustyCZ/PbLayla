Example PB config with volatile tokens for 2024-08 using TWE 3.0. When normal config gets stuck it reallocates WE to maximum one stuck position to 0.3 WE and starts using exit config for it.

When any price gets 12% away on any position it will be hedged with short position. This hedge is going to be closed in 11% distance with a loss.

1. Copy PbLayla/Examples/Example01/docker-compose.yml -> PbLayla/docker-compose.yml
2. Copy PbLayla/Examples/Example01/configs -> /home/passivbot/configs
3. Copy api-keys.json -> /home/passivbot/api-keys.json
4. Make sure you have docker image passivbot:latest v 6.1.x
5. Add api key and secret to docker-compose.yml
6. Continue with Setup and Docker setup
7. Once it is started, PbLayla should create config from template.hjson and start PB in docker container