
// PCIeDlg.h : header file
//

#pragma once


// CPCIeDlg dialog
class CPCIeDlg : public CDialogEx
{
// Construction
public:
	CPCIeDlg(CWnd* pParent = NULL);	// standard constructor

// Dialog Data
	enum { IDD = IDD_PCIE_DIALOG };

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
  afx_msg void OnBnClickedButtonDone();
  virtual BOOL PreTranslateMessage(MSG* pMsg);
  virtual void OnOK();
  afx_msg void OnBnClickedButtonClear();
  afx_msg HBRUSH OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor);
};
