@echo off
cd /d "%~dp0"

echo 正在发布便携版（自包含单文件）...
echo.

dotnet publish KeyRemap\KeyRemap.csproj -c Release -r win-x64 --self-contained true -o publish

if errorlevel 1 (
    echo.
    echo 发布失败，请检查上方的错误信息。
) else (
    echo.
    echo 完成：publish\按键映射.exe
    echo 该文件自包含 .NET 运行时，拷到任意 Windows 电脑即可直接运行。
    echo 运行时会在同目录生成 config.json，删除整个文件夹即可干净卸载。
)

echo.
pause
