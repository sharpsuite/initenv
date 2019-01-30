# initenv

Usage: `dotnet InitializeEnvironment.dll --auxiliary-partition /dev/sdX --use-downloaded-dotnet`

Sets up a minimal environment that contains the following:

* The most basic libraries required by .NET Core (glibc, libpthread, librt, etc...)
* The Linux kernel and an initramfs file
* A copy of .NET Core

Currently, initenv is only officially supported under Ubuntu 16.04. Fedora is known not to work, newer versions of Ubuntu or other distributions could work. 

Do *NOT* run this program on bare-metal hardware, as it can potentially cause data loss. Always use a VM.