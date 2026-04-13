import { Button, Card } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';

function Home() {
  return (
    <DefaultLayout>
      <div className="container mx-auto py-8">
        <Card className="p-6">
          <h1 className="text-2xl font-bold mb-4">Welcome to HeroUI3</h1>
          <p className="mb-6">This is a basic example of HeroUI3 with Vite and React Router.</p>
          <Button>Get Started</Button>
        </Card>
        <Card className="p-6">
          <h1 className="text-2xl font-bold mb-4">Welcome to HeroUI3</h1>
          <p className="mb-6">This is a basic example of HeroUI3 with Vite and React Router.</p>
          <Button>Get Started</Button>
        </Card>
        <Card className="p-6">
          <h1 className="text-2xl font-bold mb-4">Welcome to HeroUI3</h1>
          <p className="mb-6">This is a basic example of HeroUI3 with Vite and React Router.</p>
          <Button>Get Started</Button>
        </Card>
        <Card className="p-6">
          <h1 className="text-2xl font-bold mb-4">Welcome to HeroUI3</h1>
          <p className="mb-6">This is a basic example of HeroUI3 with Vite and React Router.</p>
          <Button>Get Started</Button>
        </Card>
      </div>
    </DefaultLayout>
  );
}

export default Home;

