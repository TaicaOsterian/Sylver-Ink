@echo off
if "%SdkPath%"=="" (
    set "SdkPath=C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x86"
)

set "PATH=%SdkPath%;%PATH%"

del SylverInk.msi 2>nul

copy /y .\SI_Setup\bin\x64\Release\en-US\SylverInk.msi .\

:: Dynamically extract the en-US ProductCode and inject it into the localized MSI
echo Set inst = CreateObject("WindowsInstaller.Installer") > "%temp%\sync_pc.vbs"
echo Set dbBase = inst.OpenDatabase("SylverInk.msi", 0) >> "%temp%\sync_pc.vbs"
echo Set viewBase = dbBase.OpenView("SELECT `Value` FROM `Property` WHERE `Property`='ProductCode'") >> "%temp%\sync_pc.vbs"
echo viewBase.Execute : Set rec = viewBase.Fetch : pc = rec.StringData(1) >> "%temp%\sync_pc.vbs"
echo viewBase.Close >> "%temp%\sync_pc.vbs"
echo Set dbTarget = inst.OpenDatabase(".\bin\x64\Release\es-ES\SylverInk.msi", 1) >> "%temp%\sync_pc.vbs"
echo Set viewTarget = dbTarget.OpenView("UPDATE `Property` SET `Value`='" ^& pc ^& "' WHERE `Property`='ProductCode'") >> "%temp%\sync_pc.vbs"
echo viewTarget.Execute : viewTarget.Close : dbTarget.Commit >> "%temp%\sync_pc.vbs"

cscript //nologo "%temp%\sync_pc.vbs"
del "%temp%\sync_pc.vbs"

:: Generate the transform with identical ProductCodes
msitran -g .\SylverInk.msi .\SI_Setup\bin\x64\Release\es-ES\SylverInk.msi es-ES.mst

cscript //nologo "%SdkPath%\wisubstg.vbs" .\SylverInk.msi es-ES.mst 3082

cscript //nologo "%SdkPath%\wilangid.vbs" .\SylverInk.msi Package 1033,3082

del es-ES.mst

