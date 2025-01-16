# Device Provisioning

Main documentation is found here: <https://github.com/Azure/iotedge-lorawan-starterkit/blob/docs/main/docs/tools/device-provisioning.md>.

## Build release artifacts

To build release artifacts use the PowerShell script provided (BuildForRelease.ps1).
It creats a self-container package of the cli for different platforms (Windows, Linux and MacOS).


Note: run Powershell as admin and enter 'Set-ExecutionPolicy -ExecutionPolicy RemoteSigned' in order to run the script.

## Adding Garnet as a replacement for Redis, and run it on the Edge device.

[Garnet Github](https://microsoft.github.io/garnet/docs/welcome/releases#docker)

Iot Edge deployment CreateOptions uses [Docker api for Create Container](https://docs.docker.com/reference/api/engine/version/v1.47/#tag/Container/operation/ContainerCreate) 
(`createOptions` are [passed to the container runtime through the Docker api for Create Container as-is](https://stackoverflow.com/a/59994774))

`docker create container` [creates a new container without creating it.](https://docs.docker.com/reference/cli/docker/container/create/)

`--network=host` makes [programs inside a Docker container look like they're running on the host itself.](https://stackoverflow.com/a/43317607)

