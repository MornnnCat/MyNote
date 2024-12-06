# Windows系统

## 局域网

### 文件局域共享

关于局域网共享

1标准步骤是，电脑接入同一个网络设备，在网络和共享中心-》更改高级共享设置 中，启用所有共享，然后关闭密码。

2在“启用和关闭Windows功能”中启用SMB1.0组件

3然后对一个需要共享的文件夹在高级共享和安全权限中加入Everyone，就可以了。

 

但我遇到一个问题，我的A电脑能访问B电脑但反之不行。

之后我发现，计算机管理-》服务和应用程序-》服务 中，自动并启用File History Service和Function Discovery Provider Host可以解决这个问题。

但又遇到一个问题，我的台式已经可以看到笔记本共享的文件夹了，但是需要Windows凭证才能访问，而我尝试了所有给两台电脑的“用户账户”中添加的凭证，都不奏效。

最后发现，因为我的笔记本的用户账户使用的是微软账户，所以凭证也需要微软账户的用户名和密码。



## 注册表



### 软件ID

{0DB7E03F-FC29-4DC6-9020-FF41B59E513A} 3D对象

{24ad3ad4-a569-4530-98e1-ab02f9417aa8} 图片

{088e3905-0323-4b02-9826-5d99428e115f} 下载

{3dfdf296-dbec-4fb4-81d1-6a3438bcf4de} 音乐

{B4BFCC3A-DB2C-424C-B029-7FE99A87C641} 桌面

{d3162b92-9365-467a-956b -92703aca08af} 文档

{f86fa3ab-70d2-4fc7-9c99-fcbf05467f3a} 视频

 

PowerShell获取软件与对应GUID：get-wmiobject Win32_Product | Format-Table IdentifyingNumber, Name, LocalPackage -AutoSize

HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace