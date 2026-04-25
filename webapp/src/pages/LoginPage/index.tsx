import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, Button, TextField, Label, Input } from '@heroui/react';
import { BASE_URL, setToken } from '../../services/api';
import { useI18n } from '../../i18n';

function LoginPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const res = await fetch(`${BASE_URL}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
      });

      const data = await res.json();

      if (!res.ok || !data.success) {
        setError(data.message || t('auth.loginFailed'));
        return;
      }

      setToken(data.data.token);
      navigate('/', { replace: true });
    } catch {
      setError(t('auth.loginFailed'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-50 dark:bg-gray-900">
      <Card className="w-full max-w-sm">
        <div className="p-6">
          <h1 className="text-2xl font-bold text-center mb-6">VirgoBot</h1>
          <form onSubmit={handleLogin} className="flex flex-col gap-4">
            <TextField>
              <Label>{t('auth.username')}</Label>
              <Input
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                autoComplete="username"
              />
            </TextField>
            <TextField>
              <Label>{t('auth.password')}</Label>
              <Input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
              />
            </TextField>
            {error && (
              <p className="text-sm text-red-500">{error}</p>
            )}
            <Button className="mx-auto" type="submit" variant="primary" isDisabled={loading || !username || !password}>
              {loading ? t('common.loading') : t('auth.login')}
            </Button>
          </form>
        </div>
      </Card>
    </div>
  );
}

export default LoginPage;
