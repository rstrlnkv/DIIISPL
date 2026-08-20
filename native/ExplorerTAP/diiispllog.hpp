// DIIISPL — журнал отладки для библиотеки, работающей внутри проводника.
// Copyright (C) 2026 r.strlnkv, GPLv3 (см. LICENSE в корне DIIISPL).
//
// Изнутри explorer.exe ни консоли, ни отладчика под рукой нет, поэтому пишем
// в %TEMP%\diiispl-tap.log. Файл открывается на дозапись при каждой строке —
// медленно, зато переживает падения и не требует состояния.

#pragma once
#include <windows.h>
#include <string>

// Журнал по умолчанию молчит: он пишется изнутри проводника на каждое изменение
// XAML-дерева, а это сотни строк за секунду. Включается созданием файла-маркера
// %TEMP%\diiispl-tap-debug — тогда рядом появится diiispl-tap.log.
inline bool DiiisplLogEnabled()
{
	static int enabled = -1;
	if (enabled < 0)
	{
		wchar_t dir[MAX_PATH] = {};
		if (!GetTempPathW(MAX_PATH, dir)) { enabled = 0; return false; }
		std::wstring marker = std::wstring(dir) + L"diiispl-tap-debug";
		enabled = GetFileAttributesW(marker.c_str()) != INVALID_FILE_ATTRIBUTES ? 1 : 0;
	}
	return enabled == 1;
}

inline void DiiisplLog(const std::wstring& text)
{
	if (!DiiisplLogEnabled()) return;

	wchar_t dir[MAX_PATH] = {};
	if (!GetTempPathW(MAX_PATH, dir)) return;

	std::wstring path = std::wstring(dir) + L"diiispl-tap.log";
	HANDLE file = CreateFileW(path.c_str(), FILE_APPEND_DATA,
		FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
		nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
	if (file == INVALID_HANDLE_VALUE) return;

	SYSTEMTIME now = {};
	GetLocalTime(&now);
	wchar_t stamp[64] = {};
	swprintf_s(stamp, L"%02d:%02d:%02d.%03d [pid %lu] ",
		now.wHour, now.wMinute, now.wSecond, now.wMilliseconds, GetCurrentProcessId());

	std::wstring line = std::wstring(stamp) + text + L"\r\n";
	int bytes = WideCharToMultiByte(CP_UTF8, 0, line.c_str(), -1, nullptr, 0, nullptr, nullptr);
	if (bytes > 1)
	{
		std::string utf8(static_cast<size_t>(bytes - 1), '\0');
		WideCharToMultiByte(CP_UTF8, 0, line.c_str(), -1, &utf8[0], bytes, nullptr, nullptr);
		DWORD written = 0;
		WriteFile(file, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
	}
	CloseHandle(file);
}

inline void DiiisplLogHr(const wchar_t* what, HRESULT hr)
{
	wchar_t buf[64] = {};
	swprintf_s(buf, L" = 0x%08lX", static_cast<unsigned long>(hr));
	DiiisplLog(std::wstring(what) + buf);
}
