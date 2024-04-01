// StopWatchDlg.h : header file
//

#pragma once
#include "afxwin.h"


// CStopWatchDlg dialog
class CStopWatchDlg : public CDialog
{
// Construction
public:
	CStopWatchDlg(CWnd* pParent = NULL);	// standard constructor
	BOOL check;

// Dialog Data
	enum { IDD = IDD_STOPWATCH_DIALOG };

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
	CButton m_cStart;
	CButton m_cContinue;
	CButton m_cStop;
	CButton m_cClear;
	CStatic m_cDisplay;
	afx_msg void OnBnClickedStart();
	afx_msg void OnBnClickedStop();
	afx_msg void OnBnClickedClear();
	afx_msg void OnBnClickedContinue();
	afx_msg void OnTimer(UINT nIDEvent);
	afx_msg HBRUSH OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor);
	void stopwatch();
	afx_msg void OnBnClickedButtonLap();
	CButton m_cLap;
	CStatic m_cLapDisplay;
	CListBox m_cList;
};
