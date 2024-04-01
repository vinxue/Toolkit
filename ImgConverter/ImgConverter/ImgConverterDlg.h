
// ImgConverterDlg.h : header file
//

#pragma once
#include "PictureEx.h"

// CImgConverterDlg dialog
class CImgConverterDlg : public CDialogEx
{
// Construction
public:
	CImgConverterDlg(CWnd* pParent = NULL);	// standard constructor
	CPictureEx m_Picture;	//Picture process

// Dialog Data
	enum { IDD = IDD_IMGCONVERTER_DIALOG };

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
	afx_msg void OnBnClickedButtonSrcbrowse();
	afx_msg void OnBnClickedButtonTgtbrowse();
	afx_msg void OnBnClickedButtonConvert();
	int GetEncoderClsid(const WCHAR* format, CLSID* pClsid);
	void GetSpecifiedFiles(CString strPath, CStringArray &strFileFull, CStringArray &strFileTitle);
	afx_msg void OnCbnSelchangeComboTgtformat();
	afx_msg void OnBnClickedCheckAdv();
	afx_msg void OnHScroll(UINT nSBCode, UINT nPos, CScrollBar* pScrollBar);
	int m_PerValue;
	afx_msg void OnEnChangeEditPer();
	afx_msg HBRUSH OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor);
	afx_msg void OnBnClickedButtonAbout();
	BOOL CImgConverterDlg::PreTranslateMessage(MSG* pMsg);
	afx_msg LRESULT OnNcHitTest(CPoint point);
	afx_msg void OnBnClickedButtonSingle();
	afx_msg void OnBnClickedCheckTop();
};
