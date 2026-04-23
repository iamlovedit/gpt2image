import { theme, type ThemeConfig } from 'antd';

const TECH_CYAN = '#00D4FF';
const TECH_PURPLE = '#7B61FF';
const BG_LAYOUT = '#070A13';
const BG_CONTAINER = '#10172A';
const BG_ELEVATED = '#15203A';
const BORDER = 'rgba(0, 212, 255, 0.18)';

export const techTheme: ThemeConfig = {
  algorithm: theme.darkAlgorithm,
  cssVar: true,
  token: {
    colorPrimary: TECH_CYAN,
    colorInfo: TECH_CYAN,
    colorSuccess: '#22D3A1',
    colorWarning: '#FFB13C',
    colorError: '#FF4F6D',
    colorBgLayout: BG_LAYOUT,
    colorBgContainer: BG_CONTAINER,
    colorBgElevated: BG_ELEVATED,
    colorBorder: BORDER,
    colorBorderSecondary: 'rgba(255,255,255,0.06)',
    colorText: '#E6ECF5',
    colorTextSecondary: '#8A9ABF',
    colorTextTertiary: '#5A6C91',
    fontFamily: `-apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", "Helvetica Neue", Helvetica, Arial, sans-serif`,
    fontFamilyCode: `"JetBrains Mono", "Fira Code", ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace`,
    borderRadius: 8,
    wireframe: false,
  },
  components: {
    Layout: {
      headerBg: '#0A0F1D',
      siderBg: '#0A0F1D',
      bodyBg: BG_LAYOUT,
      headerHeight: 56,
    },
    Menu: {
      darkItemBg: 'transparent',
      darkSubMenuItemBg: 'transparent',
      darkItemSelectedBg: 'rgba(0, 212, 255, 0.1)',
      darkItemSelectedColor: TECH_CYAN,
      darkItemHoverBg: 'rgba(0, 212, 255, 0.06)',
    },
    Table: {
      headerBg: '#0F1829',
      headerColor: '#8CA2C9',
      rowHoverBg: 'rgba(0, 212, 255, 0.04)',
    },
    Card: {
      colorBgContainer: BG_CONTAINER,
    },
    Button: {
      primaryShadow: '0 0 12px rgba(0, 212, 255, 0.35)',
    },
  },
};

export const accentColors = { cyan: TECH_CYAN, purple: TECH_PURPLE };
