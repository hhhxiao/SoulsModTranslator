### v2.14

- 支持文件过大时候导出为多个文件防止翻译失败

### v2.12

- 为数据库格式添加 tag 标识（文件内 id），防止出现一词多义时发生匹配错误
- 支持将简体语言文件转换为繁体，反之亦然
- 重构部分代码，提升可读性和可扩展性

### v2.10

- 重构分词规则和匹配算法，优化导出的文本格式
- 支持双语输出

### v2.7

- 优化词汇表功能，添加无视大小写
- 添加包含艾尔登法环全部人名以及地名的词汇表
- 添加导出未翻译文本时置顶中文文本的功能（使用词汇表）

### v2.4

- 添加自动构建功能

### v2.2

- 为术语表替换添加正则匹配功能
- 优化代码架构，为支持其他语言做准备

### v2.1

- 修复一个可能的闪退
- 修复黑魂 3 数据库错误和数据库导出的一个问题
