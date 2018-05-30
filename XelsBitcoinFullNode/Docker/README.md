# Xels Docker Images

Here we have some basic Docker images for testing Xels.BitcoinD and Xels.XelsD. The images build from the full node master on GitHub. After installing Docker, you can build and run the container with the following. 

# Build the Docker container 

```
cd Xels.XelsD
docker build . 
```

# Run the Docker container
```
docker run -it <containerId>
```

# Optional

You can optionally use volumes external to the Docker container so that the blockchain does not have to sync between tests. 

## Create the volume:

```
 docker volume create xelsbitcoin
```

### Run XelsD with a volume:
```
docker run --mount source=xelsbitcoin,target=/root/.xelsnode -it <containerId>
```

### Run BitcoinD with a volume:
```
docker run --mount source=xelsbitcoin,target=/root/.xelsbitcoin -it <containerId>
```

## Optionally forward ports from your localhost to the docker image

When running the image, add a `-p <containerPort>:<localPort>` to formward the ports:

```
docker run -p 37220:37220 -it <containerId>
```

## Force rebuild of docker images from master
```
docker build . --no-cache 
```

## Run image on the MainNet rather than the TestNet. 

Modify the Dockerfile to put the conf file in the right location and remove the "-testnet" from the run statement. 

``` 
---

COPY bitcoin.conf.docker /root/.xelsnode/bitcoin/Main/bitcoin.conf

--- 

CMD ["dotnet", "run"]

``` 

Also remove `testnet=1` from the `*.docker.conf` file.

