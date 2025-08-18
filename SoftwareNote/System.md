# Windows

### 常用cmd指令



1. convert 盘符:/fs:ntfs	：将该驱动器类型转换为NTFS

​	

延长暂停更新事件

计算机\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings

新建->DWORD32位值->命名：FlightSettingsMaxPauseDays->十进制：20000





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