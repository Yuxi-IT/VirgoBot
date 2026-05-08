import { useState, useEffect } from 'react';
import { Button, Spinner, Card, Link } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import { LogoGithub } from '@gravity-ui/icons';
import { useNavigate } from 'react-router-dom';

const TOTAL_STEPS = 6;

function OnboardingPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [completed, setCompleted] = useState(false);
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const res = await api.get<{ success: boolean; data: { completed: boolean } }>('/api/onboarding/status');
        if (res.data?.completed) {
          setCompleted(true);
        }
      } catch { /* not completed */ }
      setChecking(false);
    })();
  }, []);

  const finish = async () => {
    try {
      await api.post('/api/onboarding/complete', {});
    } catch { /* silent */ }
    navigate('/');
  };

  const skip = async () => {
    try {
      await api.post('/api/onboarding/complete', {});
    } catch { /* silent */ }
    navigate('/');
  };

  const steps = [
    {
      title: t('onboarding.step1'),
      desc: t('onboarding.step1Desc'),
      icon: '👋',
      body: (
        <div className="space-y-4">
          <div className="flex items-center justify-center">
            <Card className="w-full max-w-md">
              <div className="p-6 text-center space-y-3">
                <LogoGithub className="text-primary size-12 mx-auto" />
                <h2 className="text-xl font-bold">VirgoBot</h2>
                <p className="text-sm text-default-500">
                  {t('loginPage.subtitle') || '基于 .NET 10 的多通道 AI 助手框架'}
                </p>
                <Link
                  href="https://github.com/Yuxi-IT/VirgoBot"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-sm"
                >
                  GitHub
                </Link>
              </div>
            </Card>
          </div>
        </div>
      ),
    },
    {
      title: t('onboarding.step2'),
      desc: t('onboarding.step2Desc'),
      icon: '🤖',
      body: (
        <div className="space-y-2 text-sm max-w-md">
          <p>{t('agent.description') || '前往 Agent 页面，创建或导入你的 AI 助手设定。'}</p>
          <p>{t('agent.tip') || '你可以自定义角色背景、说话风格和行为模式。'}</p>
          <div className="flex gap-2 pt-2">
            <Button size="sm" onPress={() => navigate('/agents')}>
              {t('nav.agent')}
            </Button>
          </div>
        </div>
      ),
    },
    {
      title: t('onboarding.step3'),
      desc: t('onboarding.step3Desc'),
      icon: '⚙️',
      body: (
        <div className="space-y-2 text-sm max-w-md">
          <p>{t('providers.description') || '在 Providers 页面配置 LLM API 连接。'}</p>
          <p>支持 OpenAI / Anthropic / Google Gemini 兼容的 API。</p>
          <div className="flex gap-2 pt-2">
            <Button size="sm" onPress={() => navigate('/providers')}>
              {t('nav.providers')}
            </Button>
          </div>
        </div>
      ),
    },
    {
      title: t('onboarding.step4'),
      desc: t('onboarding.step4Desc'),
      icon: '🔧',
      body: (
        <div className="space-y-2 text-sm max-w-md">
          <p>{t('skills.subtitle') || 'Skills 是 VirgoBot 的核心能力扩展系统。'}</p>
          <p>支持 JSON Skill（Shell/HTTP/Scrape/Multi）和 SKILL.md 标准格式。</p>
          <div className="flex gap-2 pt-2">
            <Button size="sm" onPress={() => navigate('/skills')}>
              {t('nav.skills')}
            </Button>
          </div>
        </div>
      ),
    },
    {
      title: t('onboarding.step5'),
      desc: t('onboarding.step5Desc'),
      icon: '💬',
      body: (
        <div className="space-y-2 text-sm max-w-md">
          <p>{t('nav.chat')} — 与你的 AI 助手进行第一次对话测试。</p>
          <p>所有对话记录都会保存在本地 SQLite 数据库中。</p>
          <div className="flex gap-2 pt-2">
            <Button size="sm" onPress={() => navigate('/')}>
              {t('nav.chat')}
            </Button>
          </div>
        </div>
      ),
    },
    {
      title: t('onboarding.step6'),
      desc: t('onboarding.step6Desc'),
      icon: '🚀',
      body: (
        <div className="space-y-2 text-sm max-w-md">
          <ul className="list-disc list-inside space-y-1">
            <li>自动回复 — 空闲时主动发起对话</li>
            <li>语音对话 — 语音输入和输出</li>
            <li>Telegram 频道 — 通过 TG Bot 交互</li>
            <li>定时任务 — 自动化定期操作</li>
            <li>MCP 集成 — 连接外部工具服务</li>
          </ul>
        </div>
      ),
    },
  ];

  if (checking) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]">
          <Spinner size="lg" />
        </div>
      </DefaultLayout>
    );
  }

  if (completed) {
    return (
      <DefaultLayout>
        <div className="container mx-auto p-4 text-center py-16">
          <h1 className="text-2xl font-bold mb-4">{t('onboarding.title')}</h1>
          <p className="text-default-500 mb-6">{t('onboarding.completed')}</p>
          <Button onPress={() => navigate('/')}>{t('nav.dashboard')}</Button>
        </div>
      </DefaultLayout>
    );
  }

  const current = steps[step];

  return (
    <DefaultLayout>
      <div className="container max-w-2xl mx-auto p-4">
        {/* Progress indicator */}
        <div className="flex justify-center gap-2 mb-8">
          {steps.map((_, i) => (
            <div
              key={i}
              className={`h-2 flex-1 rounded-full transition-colors ${
                i <= step ? 'bg-primary' : 'bg-default-200'
              }`}
            />
          ))}
        </div>

        {/* Step content */}
        <Card>
          <div className="p-8 text-center">
            <div className="text-5xl mb-4">{current.icon}</div>
            <h2 className="text-2xl font-bold mb-2">{current.title}</h2>
            <p className="text-default-500 mb-6">{current.desc}</p>
            <div className="flex justify-center">{current.body}</div>
          </div>
        </Card>

        {/* Navigation */}
        <div className="flex justify-between mt-6">
          <Button variant="ghost" onPress={skip}>
            {t('onboarding.skip')}
          </Button>
          <div className="flex gap-2">
            {step > 0 && (
              <Button variant="secondary" onPress={() => setStep(step - 1)}>
                {t('onboarding.back')}
              </Button>
            )}
            {step < TOTAL_STEPS - 1 ? (
              <Button onPress={() => setStep(step + 1)}>
                {t('onboarding.next')}
              </Button>
            ) : (
              <Button onPress={finish}>
                {t('onboarding.finish')}
              </Button>
            )}
          </div>
        </div>
      </div>
    </DefaultLayout>
  );
}

export default OnboardingPage;
