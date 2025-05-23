
// BitViewerDlg.h : header file
//

#pragma once

//#define ACRYLIC_SUPPORT

// CBitViewerDlg dialog
class CBitViewerDlg : public CDialogEx
{
// Construction
public:
	CBitViewerDlg(CWnd* pParent = nullptr);	// standard constructor

// Dialog Data
#ifdef AFX_DESIGN_TIME
	enum { IDD = IDD_BITVIEWER_DIALOG };
#endif

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
	afx_msg void OnBnClickedButtonDecode();
	afx_msg HBRUSH OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor);
	virtual BOOL PreTranslateMessage(MSG* pMsg);
	afx_msg void OnBnClickedOk();
	afx_msg void OnBnClickedCheckSetBitfield();
	afx_msg void OnBnClickedButtonSetBitfield();
	void EncodeHexValue(UINT8 SpecialIndex, UINT8 SpecialVal);
	UINT8 NegationValue(UINT8 Index);
#ifdef ACRYLIC_SUPPORT
private:
	CMFCButton m_btnDecode;
	CMFCButton m_btnSetBitfield;
#endif
};
