# HeroUI 3 Vite Template

基于 HeroUI 3、React 19、Vite 5 和 TypeScript 的现代化模板，内置国际化、主题切换和底部导航栏。

## 快速开始

```bash
npm install
npm run dev
```

## 核心功能

### 🌐 国际化 (i18n)
- 自动检测系统语言
- 支持中文/英文切换
- 配置文件：`src/i18n/locales/`

**添加翻译：**
编辑 `src/i18n/locales/en.ts` 和 `zh.ts`

```typescript
export default {
  nav: { home: 'Home' },
  common: { welcome: 'Welcome' }
};
```

**使用：**
```tsx
import { useI18n } from './i18n';

const { t } = useI18n();
<p>{t('nav.home')}</p>
```

### 🎨 主题切换
- 自动检测系统主题
- 支持明亮/暗黑模式
- View Transitions API 动画

**使用：**
```tsx
import { useTheme } from './theme';

const { theme, setTheme } = useTheme();
setTheme('dark');
```

### 🧭 导航配置

**修改导航项：**
编辑 `src/config/site.ts`

```typescript
import { Home, User, Mail } from '@gravity-ui/icons';

export const navItems = [
  { label: 'Home', url: '/', icon: Home },
  { label: 'Square', url: '/square', icon: User },
  { label: 'Contact', url: '/contact', icon: Mail }
];
```

**添加新页面：**
1. 在 `src/pages/` 创建组件
2. 在 `src/routes/index.tsx` 添加路由
3. 在 `src/config/site.ts` 添加导航项

### 📱 底部导航栏
- 仅在移动端显示（`sm:hidden`）
- 自动跟随路由切换
- 流畅的动画效果

## 技术栈

- **UI 框架**: [HeroUI 3](https://v3.heroui.com/docs/react/getting-started)
- **图标库**: [@gravity-ui/icons](https://github.com/gravity-ui/icons)
- **动画库**: [Framer Motion](https://www.framer.com/motion/)
- **路由**: React Router DOM v7
- **样式**: Tailwind CSS v4

## 项目结构

```
src/
├── components/     # 组件
│   ├── Navbar.tsx
│   └── BottomNav.tsx
├── pages/          # 页面
├── routes/         # 路由配置
├── config/         # 配置文件
│   └── site.ts     # 导航配置
├── i18n/           # 国际化
│   ├── index.tsx
│   └── locales/
│       ├── en.ts
│       └── zh.ts
└── theme/          # 主题
    └── index.tsx
```

## 常用命令

```bash
npm run dev      # 开发服务器
npm run build    # 生产构建
npm run preview  # 预览构建
npm run lint     # 代码检查
```

## 文档链接

- [HeroUI 3 文档](https://v3.heroui.com/docs/react/getting-started)
- [Gravity UI Icons](https://gravity-ui.com/icons)
- [Framer Motion](https://www.framer.com/motion/)
- [React Router](https://reactrouter.com/)
