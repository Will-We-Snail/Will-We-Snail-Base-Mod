@echo off
call .\_set_game_dir.bat
cd "..\..\"
md "libs"
mklink /j "libs\gmml" "%GAME_DIR%\gmml"
