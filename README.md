# PterodactylDiscord
A small utility software intended to be run as a Pterodactyl server to manage other Pterodactyl servers and nodes. 

_This software is currently designed for a specific use case, applying it on other configurations needs additional manual work on the target system and maybe source code changes_

The application is exposed as a Discord Bot, allowing to manage (start, stop, restart, kill) Pterodactyl servers. This also includes a automatic shutdown function, if the server is perceived empty for a specified amount of time. This is currently based on a configurable amount of data a server needs to send/receive.
If no servers are running the application is also able to send a stop command to the node through ssh, to shutdown the physical server.

## Future Plans
- Implement interactions to server consoles, may be used for
  - Chat integration to discord
  - Custom commands
  - Improved empty server logic
- Allow configuration of multiple nodes
