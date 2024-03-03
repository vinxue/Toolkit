
// KbSimDlg.h : header file
//

#pragma once


// CKbSimDlg dialog
class CKbSimDlg : public CDialogEx
{
// Construction
public:
	CKbSimDlg(CWnd* pParent = NULL);	// standard constructor

// Dialog Data
	enum { IDD = IDD_KbSim_DIALOG };

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
  afx_msg void OnTimer(UINT_PTR nIDEvent);
  afx_msg void OnBnClickedButtonRun();
  afx_msg void OnBnClickedButtonStop();
  afx_msg HBRUSH OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor);
};
