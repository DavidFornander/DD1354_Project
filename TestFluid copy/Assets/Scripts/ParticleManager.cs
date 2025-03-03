using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Mathematics;

public class ParticleManager : MonoBehaviour
{
    public GameObject objectPrefab; // Reference to object prefab
    private Simulation[] objects;

    public int numParticles;
    public float particleSpacing = 0.25f;
    float radius = 0.5f;
    const float mass = 1;
    public float gravity = 0f;

    public float targetDensity = 2.75f;
    public float smoothingRadius = 0.5f;
    public float pressureMultiplier = 0.5f;
    public float viscosityStrength = 0.2f;
    // Mouse interactions
    public float interactionStrength = 0f;
    public float interactionRadius = 3f;

    // Start is called before the first frame update
    void Start()
    {
        // Skapa objekten och placera ut dem i ett grid i början av simuleringen
        objects = new Simulation[numParticles];

        int particlesPerRow = (int) math.sqrt(numParticles);
        int particlesPerCol = (numParticles - 1) / particlesPerRow + 1;

        for(int i = 0; i < numParticles; i++) 
        {
            float x = ((i % (float)particlesPerRow) - ((float)particlesPerRow / 2f)) * radius;
            float y = ((i / (float)particlesPerRow) - ((float)particlesPerCol / 2f)) * radius;

            GameObject clone = Instantiate(objectPrefab, new Vector2(x, y), Quaternion.identity);
            objects[i] = clone.GetComponent<Simulation>(); // Store reference
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Effect of gravity on velocity
        //Vector2 velocity = objects[i].getVelocity();
        //velocity *= 0.9f;   // ta bort inertia lite
        //velocity += Vector2.down * gravity * Time.deltaTime;

        // Density calculation from predicted position
        for(int i = 0 ; i < numParticles; i++) 
        {
            // Predict position to use in density calculation
            Vector2 position = objects[i].getPosition();
            Vector2 velocity = objects[i].getVelocity();
            Vector2 predictedPosition = position + velocity * Time.deltaTime;
            objects[i].setPredictedPosition(predictedPosition);

            // Density around a point
            float density = CalculateDensity(predictedPosition);
            //Debug.Log("Density: "+ density + "for particle: "+ i);
            objects[i].setDensity(density);
        }
        // Add viscosity force and update velocity
        for(int i = 0 ; i < numParticles; i++) 
        {
            Vector2 viscosityForce = CalculateViscosityForce(i);
            Vector2 viscosityAcceleration = viscosityForce / objects[i].getDensity();

            Vector2 velocity = objects[i].getVelocity();
            velocity += viscosityAcceleration * Time.deltaTime;
            objects[i].setVelocity(velocity);

        }
        // Add pressure force
        for(int i = 0 ; i < numParticles; i++) 
        {
            Vector2 pressureForce = CalculatePressureForce(i);
            objects[i].setPressureForce(pressureForce);
        }
        // Update velocity and position and handle collisions
        for(int i = 0 ; i < numParticles; i++) 
        {
            // InteractionForce(mouse.pos, interactionRadius, interactionStrength, i);

            Vector2 pressureAcceleration = objects[i].getPressureForce() / objects[i].getDensity();

            Vector2 velocity = objects[i].getVelocity();
            Vector2 position = objects[i].getPosition();

            velocity += pressureAcceleration * Time.deltaTime;
            position += velocity * Time.deltaTime;
            objects[i].setPosition(position);
            objects[i].setVelocity(velocity);

            objects[i].ResolveCollisions();
        }
        
    }



    float CalculateDensity(Vector2 particlePosition) {
        float density = 0;

        foreach (Simulation obj in objects) 
        {
            Vector2 position = obj.getPosition();
            float dist = (position - particlePosition).magnitude;
            float influence = SmoothingKernel(dist, smoothingRadius);
            density += mass * influence;
        }
        return density;
    }

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 pressureForce = Vector2.zero;

        for(int otherParticleIndex = 0; otherParticleIndex < numParticles; otherParticleIndex++)
        {
            //Hoppas över partikeln vi räknar på
            if(particleIndex == otherParticleIndex) continue;

            Vector2 offset = objects[otherParticleIndex].getPosition() - objects[particleIndex].getPosition();
            float dist = offset.magnitude;
            // Se till att inte dela på 0
            Vector2 dir = (dist <= 0) ? getRandomDir() : (offset / dist);

            float slope = SmoothingKernelDerivative(dist, smoothingRadius);
            float otherDensity = objects[otherParticleIndex].getDensity();
            float density = objects[particleIndex].getDensity();
            float sharedPressure = CalculateSharedPressure(otherDensity, density);

            // Puttar bort partikeln från andra partiklar
            pressureForce += -sharedPressure * dir * slope * mass / otherDensity;
        }

        return pressureForce;
    }

    Vector2 CalculateViscosityForce(int particleIndex)
    {
        Vector2 viscostityForce = Vector2.zero;
        Vector2 position = objects[particleIndex].getPosition();

        for(int otherParticleIndex = 0; otherParticleIndex < numParticles; otherParticleIndex++)
        {
            float dst = (position - objects[otherParticleIndex].getPosition()).magnitude;
            float influence = ViscositySmoothingKernel(dst, smoothingRadius);
            viscostityForce += (objects[otherParticleIndex].getVelocity() - objects[particleIndex].getVelocity()) * influence;
        }
        return viscostityForce * viscosityStrength;
    }



    Vector2 getRandomDir() 
    {
        float x = UnityEngine.Random.Range(-1, 1);
        float y = UnityEngine.Random.Range(-1, 1);
        return new Vector2(x, y);
    }

    // Gör att partiklarna puttar på varandra istället för att ena puttar på den andra 
    float CalculateSharedPressure(float densityA, float densityB)
    {
        float pressureA = ConvertDensityToPressure(densityA);
        float pressureB = ConvertDensityToPressure(densityB);
        return (pressureA + pressureB) / 2;
    }

    // hur mycket en partikel påverkar en annan beroende på deras avstånd
    static float SmoothingKernel(float dist, float radius) 
    {
        if(dist >= radius) return 0;

        float volume = math.PI * math.pow(radius, 4) / 6;
        float value = radius - dist;
        return value * value / volume;
    }

    static float SmoothingKernelDerivative(float dist, float radius) 
    {
        if(dist >= radius) return 0;

        float scale = 12 / (math.pow(radius, 4) * math.PI);
        float value = radius - dist;
        return value * scale;
    }


    // hur mycket en partikel påverkar en annan beroende på deras avstånd (specifikt för viscosity)
    static float ViscositySmoothingKernel(float dist, float radius) 
    {
        if(dist >= radius) return 0;

        float volume = math.PI * math.pow(radius, 4) / 6;
        float value = radius * radius - dist * dist;
        return value * value / volume;
    }

    // Fitting for gasses
    float ConvertDensityToPressure(float density)
    {
        float densityError = density - targetDensity;
        float pressure = densityError * pressureMultiplier;
        return pressure;
    }







    // Mouse interaction
    // Vector2 InteractionForce(Vector2 inputPos, float radius, float strength, int particleIndex)
    // {
    //     Vector2 InteractionForce = Vector2.zero;
    //     Vector2 offset = inputPos - objects[particleIndex].getPosition();
    //     float sqrDst = Vector2.Dot(offset, offset);

    //     if(sqrDst < radius * radius)
    //     {
    //         float dst = math.sqrt(sqrDst);
    //         Vector2 dirToInputPoint = dst <= float.Epsilon ? Vector2.zero : offset/dst;
    //         float centreT = 1 - dst / radius;

    //         InteractionForce += (dirToInputPoint * strength - objects[particleIndex].getVelocity()) * centreT;
    //     }

    //     return InteractionForce;
    // }

}
