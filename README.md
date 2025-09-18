# Base Configuration

On all servers:

1.  install Docker and configure a Swapfile

```
sudo fallocate -l 1G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

# Add Docker's official GPG key:
sudo apt-get update
sudo apt-get install ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

# Add the repository to Apt sources:
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update

sudo apt-get install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin -y
```

2. Set the hostname

```
sudo hostnamectl set-hostname <hostname>
```

3. Setup Tailscale using the generated command!

4. On the master server run, this will generate a command you can run on the nodes.

```
docker swarm init --advertise-addr <TailscaleIP>
```

You can then edit the `docker-deploy.yml` file to a suitable number of replicas, run `docker stack deploy -c docker-deploy.yml <STACK>` on the server to create instances of the client across the nodes.

Check that everything is running with `docker service ls` and `docker service ps <SERVICE>`.

Inspect the number of replicas per node with `docker service ps <SERVICE> --filter desired-state=running --format '{{.Node}}' | sort | uniq -c`.

Increase or decrease the number of replicas with `docker service scale <SERVICE>=<N>`.

# Server Instance

To start the main server instance simply run `docker compose --profile server up -d` on the master server. This will init everything we need, make sure this server has a couple cores, GB of RAM and space to spare. 