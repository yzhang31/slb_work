// ForceCrash.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include <stdio.h>
#include <tchar.h>
#include <Windows.h>
#include <vector>
#include <string>
#include <iostream>

using namespace std;

BOOL setCurrentPrivilege(BOOL bEnable, LPCTSTR lpszPrivilege)
{
	HANDLE hToken = 0;
	if (::OpenThreadToken(::GetCurrentThread(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, FALSE, &hToken)
		|| ::OpenProcessToken(::GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &hToken))
	{
		TOKEN_PRIVILEGES tp;
		LUID luid;

		if (!::LookupPrivilegeValue(
			NULL,            // lookup privilege on local system
			lpszPrivilege,   // privilege to lookup 
			&luid))        // receives LUID of privilege
		{
			::CloseHandle(hToken);
			return FALSE;
		}
		tp.PrivilegeCount = 1;
		tp.Privileges[0].Luid = luid;
		tp.Privileges[0].Attributes = (bEnable) ? SE_PRIVILEGE_ENABLED : 0;

		// Enable the privilege or disable all privileges.
		if (!::AdjustTokenPrivileges(
			hToken,
			FALSE,
			&tp,
			sizeof(TOKEN_PRIVILEGES),
			(PTOKEN_PRIVILEGES)NULL,
			(PDWORD)NULL)
			)
		{
			CloseHandle(hToken);
			return FALSE;
		}
		::CloseHandle(hToken);
	}
	return TRUE;
}

int killProcess(DWORD processID)
{
	HANDLE hProcess = ::OpenProcess(PROCESS_ALL_ACCESS, FALSE, processID);
	if (hProcess)
	{
		if (!setCurrentPrivilege(TRUE, SE_DEBUG_NAME))
		{
			cout <<"Could not enable debug privilege" << endl;
		}

		HANDLE hThread = ::CreateRemoteThread(hProcess, NULL, 0, (LPTHREAD_START_ROUTINE)1, NULL, 0, NULL);
		if (hThread)
		{
			::CloseHandle(hThread);
			::CloseHandle(hProcess);
			cout << "Success inject crash into process." << endl;
			return 0;
		}
		else
		{
			_tprintf(TEXT("Error: %d\n"), GetLastError());
			::CloseHandle(hProcess);
			cout << "Failed to inject crash into process." << endl;
			return 1;
		}
	}
	else
	{
		cout << "Can not open process with pid: "<< processID << endl;
		return -1;
	}
}

int __cdecl _tmain(int argc, _TCHAR *argv[])
{
	vector<wstring> args(argv + 1, argv + argc);

	if (args.size() == 0) {
		cout << "Syntax: ForceCrash -p <PID>" << endl;
		return 0;
	}

	for (auto i = args.begin(); i != args.end(); ++i) {
		if (*i == L"-h" || *i == L"--help") {
			cout << "Syntax: ForceCrash -p <PID>" << endl;
		}
		else if (*i == L"-p") {
			wstring pid = *++i;
			int res = killProcess(stoi(pid));
		}
	}
	return 0;
}
