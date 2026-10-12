import type { Topic } from '../types'

/*
 * Stability (S) values are calibrated so calculateRetention() lands on the
 * intended risk band:
 *   Trigonometri      t=11, S=14 → 46% (high_risk)
 *   Hücre Bölünmeleri t=6,  S=10 → 55% (review_soon)
 *   Sözcükte Anlam    t=3,  S=20 → 86% (safe)
 *   Fonksiyonlar      t=8,  S=12 → 51% (review_soon)
 */

export const featuredTopic: Topic = {
  id: 'ayt-mat-trigonometri',
  title: 'Trigonometri',
  category: 'AYT Matematik',
  lastStudiedDaysAgo: 11,
  previousSuccessRate: 0.72,
  reviewCount: 1,
  currentStrength: 14,
  quiz: [
    {
      id: 'trig-q1',
      question: 'sin 30° · cos 60° + cos 30° · sin 60° işleminin sonucu kaçtır?',
      options: ['1/2', '√3/2', '0', '1'],
      correctIndex: 3,
    },
    {
      id: 'trig-q2',
      question: 'x dar açı ve tan x = 3/4 olduğuna göre, sin x kaçtır?',
      options: ['3/5', '4/5', '3/4', '5/3'],
      correctIndex: 0,
    },
    {
      id: 'trig-q3',
      question: 'cos 2x ifadesi aşağıdakilerden hangisine eşittir?',
      options: ['2 · sin x · cos x', '1 − 2sin²x', 'sin²x + cos²x', '2cos²x + 1'],
      correctIndex: 1,
    },
  ],
}

export const auxiliaryTopics: Topic[] = [
  {
    id: 'tyt-bio-hucre-bolunmeleri',
    title: 'Hücre Bölünmeleri',
    category: 'TYT Biyoloji',
    lastStudiedDaysAgo: 6,
    previousSuccessRate: 0.65,
    reviewCount: 2,
    currentStrength: 10,
  },
  {
    id: 'tyt-tur-sozcukte-anlam',
    title: 'Sözcükte Anlam',
    category: 'TYT Türkçe',
    lastStudiedDaysAgo: 3,
    previousSuccessRate: 0.88,
    reviewCount: 4,
    currentStrength: 20,
  },
  {
    id: 'ayt-mat-fonksiyonlar',
    title: 'Fonksiyonlar',
    category: 'AYT Matematik',
    lastStudiedDaysAgo: 8,
    previousSuccessRate: 0.6,
    reviewCount: 2,
    currentStrength: 12,
  },
]

export const mockTopics: Topic[] = [featuredTopic, ...auxiliaryTopics]
