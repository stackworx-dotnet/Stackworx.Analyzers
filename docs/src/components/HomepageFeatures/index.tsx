import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  description: ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: '🎯 Architectural Enforcement',
    description: (
      <>
        Enforce clean architecture patterns and maintain clear feature boundaries
        in your C# projects. Prevent unauthorized namespace access and ensure
        consistent code organization.
      </>
    ),
  },
  {
    title: '⚡ GraphQL Optimization',
    description: (
      <>
        Specialized rules for HotChocolate GraphQL development including DataLoader
        validation, extension class requirements, and resolver patterns.
      </>
    ),
  },
  {
    title: '🛡️ Type Safety',
    description: (
      <>
        Catch common pitfalls with DateTime handling, implicit conversions, and
        other semantic issues. Improve code quality and prevent subtle bugs.
      </>
    ),
  },
];

function Feature({title, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
