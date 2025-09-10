# Windows

### 常用cmd指令



1. convert 盘符:/fs:ntfs	：将该驱动器类型转换为NTFS

​	

延长暂停更新事件

计算机\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings

新建->DWORD32位值->命名：FlightSettingsMaxPauseDays->十进制：20000



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

# Clash规则



rules:

\- 'DOMAIN-SUFFIX,*.xxx.io,REJECT' 
#表示拒绝访问任何以.xxx.io结尾的域名

\- 'DOMAIN-SUFFIX,xxx.io,REJECT'
#表示只拒绝访问xxx.io

\- 'DOMAIN-SUFFIX,ic.adobe.io,REJECT-DROP'



- REJECT：拒绝该请求，当连接类型为 HTTP 时，会返回一个错误页面。（该行为可被 show-error-page-for-reject 参数控制）
- REJECT-TINYGIF：拒绝该请求，当连接类型为 HTTP 时，返回一个 1px 的 GIF 图片响应。若为其他类型连接则直接断开。该策略主要用于 Web 广告屏蔽。
- REJECT-DROP：拒绝该请求，与 REJECT 不同的是，该策略将静默抛弃请求。因为部分程序有着十分暴力的重试逻辑，连接失败后会立刻进行重试，导致请求风暴。
- REJECT-NO-DROP：一般情况下与 REJECT 策略相同，区别在于使用该规则时将不会触发上述自动升级的行为。



### Adobe警告窗口

它弹出警告窗口是因为连接梯子后远程识别到盗版，禁止它通过梯子访问即可

rules:

​    \- 'DOMAIN-SUFFIX,*.adobe.io,REJECT'

​    \- 'DOMAIN-SUFFIX,*.adobestats.io,REJECT'

​    \- 'DOMAIN-SUFFIX,ic.adobe.io,REJECT'

## win11关闭二级右键菜单

cmd命令，前者为关闭二级菜单，后者为恢复。

```cmd
reg add "HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32" /f /ve

reg delete "HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}" /f
```

# Rime-小狼毫

文档：https://github.com/rime/home/wiki/CustomizationGuide

git地址：https://github.com/rime/weasel

雾凇拼音：https://github.com/iDvel/rime-ice

雾凇拼音配置导入：

1. 下载好小狼毫后进入用户文件夹

2. 在用户文件夹中clone或pull雾凇拼音的最新词库
   ```
   git clone https://github.com/iDvel/rime-ice.git Rime --depth 1
   
   # 更新
   cd Rime
   git pull
   ```

   *注意需要将仓库的内容直接放置在用户文件夹下
