# NOS Clicker

Минималистичный автокликер для Windows 10/11 с лаймовыми акцентами.

## Скачать

Откройте [последний релиз](https://github.com/NOSFERATU-ll/Nos_clicker/releases/latest) и скачайте **NosClicker.exe**. Установка не нужна. Использует встроенный в Windows .NET Framework 4.x.

## Управление

- ЛКМ / ПКМ — выбор кнопки мыши.
- 1–1000 CPS — верхняя целевая частота. Реальная скорость зависит от Windows и приложения.
- «Без лимита» — без программного ограничения частоты; загрузка процессора может вырасти.
- F6 — глобальный запуск / остановка. Нажмите кнопку клавиши и новую клавишу для переназначения (F1–F12, буквы, цифры, пробел, Insert, Home, End).
- Esc — аварийная остановка; при выборе клавиши отменяет выбор.
- Наведите курсор на нужное приложение: клики идут в текущую позицию курсора. Когда окно кликера активно, отправка кликов приостановлена.
- Настройки сохраняются в `%LOCALAPPDATA%\NosClicker\settings.txt` при закрытии.

Windows не разрешает обычному процессу отправлять ввод в приложения с более высокими правами. Некоторые игры могут игнорировать синтетический ввод. Файл не имеет цифровой подписи.

## Сборка

В PowerShell из корня репозитория:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:NosClicker.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\NosClicker.cs
$p = Start-Process .\NosClicker.exe -ArgumentList '--self-test' -Wait -PassThru
if ($p.ExitCode -ne 0) { throw 'Self-test failed' }
```

GitHub Actions компилирует приложение, запускает проверки структуры Windows INPUT, флагов кнопок, границ CPS и повторной остановки, затем публикует EXE и SHA-256 в релизе v1.0.0. Проверки не отправляют реальные клики.
