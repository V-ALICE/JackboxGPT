@echo off

set VERBOSITY=warning
if not [%3]==[] set VERBOSITY=%3
for /l %%x in (1, 1, %1) do start cmd.exe @cmd /k "JackboxGPT3.exe --verbosity %VERBOSITY% --name AI-%%x --engine curie %2"