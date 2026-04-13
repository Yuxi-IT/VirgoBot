import { Button, Card } from '@heroui/react';
import { useNavigate } from 'react-router-dom';
import { useEffect } from 'react';
import { useI18n } from '../../i18n';
import { setTitle } from '../../App';

export default function NotFound() {
  const navigate = useNavigate();
  const { t } = useI18n();
  useEffect(() => {
    setTitle('404');
  }, []);

  return (
    <div className="flex items-center justify-center min-h-screen p-4">
      <Card className="max-w-md w-full">
        <div className="text-center py-10">
          <h1 className="text-6xl font-bold mb-4">404</h1>
          <p className="text-xl mb-6">{t('notFound.description')}</p>
          <Button onPress={() => navigate('/')}>
            {t('notFound.button')}
          </Button>
        </div>
      </Card>
    </div>
  );
}
