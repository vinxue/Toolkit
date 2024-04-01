// TimerDlg.h : header file
//

#pragma once
#include "afxdtctl.h"
#include "afxwin.h"


// CTimerDlg dialog
class CTimerDlg : public CDialog
{
// Construction
public:
	CTimerDlg(CWnd* pParent = NULL);	// standard constructor

// Dialog Data
	enum { IDD = IDD_TIMER_DIALOG };

	protected:
	virtual void DoDataExchange(CDataExchange* pDX);	// DDX/DDV support


// Implementation
protected:
	HICON m_hIcon;

	// Generated message map functions
	virtual BOOL OnInitDialog();
	afx_msg void OnSysCommand(UINT nID, LPARAM lParam);
	afx_msg void OnPaint();
	afx_msg HCURSOR OnQueryDragIcon();
	DECLARE_MESSAGE_MAP()
public:
	CDateTimeCtrl m_Date;
	CDateTimeCtrl m_Time;
	afx_msg void OnTimer(UINT_PTR nIDEvent);
	afx_msg void OnBnClickedButtonActivate();
	afx_msg void OnBnClickedButtonBrowse();
	CString m_strFile;
	CButton m_cActivate;
	CButton m_cPause;
	afx_msg void OnBnClickedButtonPause();
	afx_msg void OnBnClickedButtonAbout();
	CDateTimeCtrl m_Count;
	DWORD TargetCount;
	DWORD Count;
};
