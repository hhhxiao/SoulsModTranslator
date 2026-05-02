# AGENTS.md

## 简介

这是一个游戏mod翻译工具，读取特定目录下的dcx文件，和数据库比对后导出未翻译的文本，在翻译后重新导入能生成翻译后的dcx文件

## 架构

分三个模块

- SMT.Core - 文件读取，核心算法
- SMT.WPF - GUI
- SMT.Console - 控制台版本（有待开发）

## 编译

一般都是直接编译和运行SMT.WPF，在SMT.WPF目录下运行

```
dotnet build
```

## 编译+运行

一般都是直接编译和运行SMT.WPF，在SMT.WPF目录下运行，如果只是指定目录而不在这个目录下的话会导致找不到相关数据

```
dotnet run
```
